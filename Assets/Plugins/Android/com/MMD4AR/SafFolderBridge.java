package com.MMD4AR.filepicker;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.UriPermission;
import android.database.Cursor;
import android.net.Uri;
import android.provider.DocumentsContract;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;

public class SafFolderBridge {

    // C#側のコールバック先メソッド名(GameObjectに用意する)
    private static final String CB_PROGRESS  = "OnSafFolderProgress";
    private static final String CB_COMPLETED = "OnSafFolderCompleted";
    private static final String CB_CANCELLED = "OnSafFolderCancelled";
    private static final String CB_ERROR     = "OnSafFolderError";

    private static final String PREFS = "saf_folder_bridge";
    private static final String KEY_TREE_URI = "tree_uri";

    private static final AtomicBoolean busy = new AtomicBoolean(false);

    // ===== C#から呼ぶAPI =====

    /** フォルダ選択ダイアログを開き、選択後にアプリ内へコピーする */
    public static void pickFolder(String callbackObject, String destRoot) {
        if (busy.get()) {
            send(callbackObject, CB_ERROR, "busy");
            return;
        }
        Activity activity = UnityPlayer.currentActivity;
        Intent intent = new Intent(activity, SafFolderPickerActivity.class);
        intent.putExtra(SafFolderPickerActivity.EXTRA_CALLBACK, callbackObject);
        intent.putExtra(SafFolderPickerActivity.EXTRA_DEST_ROOT, destRoot);
        activity.startActivity(intent);
    }

    /** 以前選んだフォルダ(永続化済み)をダイアログなしで再コピーする */
    public static void copyFromSavedTree(String callbackObject, String destRoot) {
        Activity activity = UnityPlayer.currentActivity;
        Uri uri = getSavedTreeUri(activity);
        if (uri == null || !hasPersistedPermission(activity, uri)) {
            send(callbackObject, CB_ERROR, "no_saved_tree");
            return;
        }
        startCopy(activity.getApplicationContext(), uri, callbackObject, destRoot);
    }

    public static boolean hasSavedTree() {
        Activity activity = UnityPlayer.currentActivity;
        Uri uri = getSavedTreeUri(activity);
        return uri != null && hasPersistedPermission(activity, uri);
    }

    public static void clearSavedTree() {
        Activity activity = UnityPlayer.currentActivity;
        Uri uri = getSavedTreeUri(activity);
        if (uri != null) {
            try {
                activity.getContentResolver().releasePersistableUriPermission(
                        uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } catch (Exception ignored) { }
        }
        prefs(activity).edit().remove(KEY_TREE_URI).apply();
    }

    // ===== Activityから使う内部API =====

    static void saveTreeUri(Context ctx, Uri uri) {
        prefs(ctx).edit().putString(KEY_TREE_URI, uri.toString()).apply();
    }

    static void send(String obj, String method, String msg) {
        if (obj == null) return;
        UnityPlayer.UnitySendMessage(obj, method, msg == null ? "" : msg);
    }

    static void notifyCancelled(String obj) { send(obj, CB_CANCELLED, ""); }
    static void notifyError(String obj, String msg) { send(obj, CB_ERROR, msg); }

    static void startCopy(final Context ctx, final Uri treeUri,
                          final String callbackObject, final String destRoot) {
        if (!busy.compareAndSet(false, true)) {
            send(callbackObject, CB_ERROR, "busy");
            return;
        }
        new Thread(new Runnable() {
            @Override public void run() {
                try {
                    ContentResolver cr = ctx.getContentResolver();
                    String rootDocId = DocumentsContract.getTreeDocumentId(treeUri);

                    String folderName = queryDisplayName(cr, treeUri, rootDocId);
                    if (folderName == null || folderName.isEmpty()) folderName = "picked_folder";
                    folderName = sanitize(folderName);

                    File dest = new File(destRoot, folderName);
                    deleteRecursive(dest);
                    if (!dest.mkdirs() && !dest.isDirectory()) {
                        throw new IOException("mkdirs failed: " + dest.getAbsolutePath());
                    }

                    int[] count = new int[]{0};
                    copyChildren(cr, treeUri, rootDocId, dest, count, callbackObject);

                    send(callbackObject, CB_COMPLETED, dest.getAbsolutePath());
                } catch (Exception e) {
                    send(callbackObject, CB_ERROR, e.getClass().getSimpleName() + ": " + e.getMessage());
                } finally {
                    busy.set(false);
                }
            }
        }, "SafFolderCopy").start();
    }

    // ===== コピー処理 =====

    private static class Entry {
        String docId; String name; boolean isDir;
    }

    private static void copyChildren(ContentResolver cr, Uri treeUri, String parentDocId,
                                     File destDir, int[] count, String cb) throws IOException {
        Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(treeUri, parentDocId);
        List<Entry> entries = new ArrayList<Entry>();

        Cursor c = cr.query(childrenUri, new String[]{
                DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                DocumentsContract.Document.COLUMN_MIME_TYPE}, null, null, null);
        if (c == null) throw new IOException("query failed: " + childrenUri);
        try {
            while (c.moveToNext()) {
                Entry e = new Entry();
                e.docId = c.getString(0);
                e.name = sanitize(c.getString(1));
                e.isDir = DocumentsContract.Document.MIME_TYPE_DIR.equals(c.getString(2));
                entries.add(e);
            }
        } finally {
            c.close();
        }

        for (Entry e : entries) {
            if (e.name.equals(".") || e.name.equals("..") || e.name.isEmpty()) continue;
            File out = new File(destDir, e.name);
            if (e.isDir) {
                if (!out.mkdirs() && !out.isDirectory()) {
                    throw new IOException("mkdirs failed: " + out.getAbsolutePath());
                }
                copyChildren(cr, treeUri, e.docId, out, count, cb);
            } else {
                Uri fileUri = DocumentsContract.buildDocumentUriUsingTree(treeUri, e.docId);
                copyFile(cr, fileUri, out);
                count[0]++;
                if (count[0] % 10 == 0) send(cb, CB_PROGRESS, String.valueOf(count[0]));
            }
        }
    }

    private static void copyFile(ContentResolver cr, Uri src, File dest) throws IOException {
        InputStream in = cr.openInputStream(src);
        if (in == null) throw new IOException("openInputStream null: " + src);
        OutputStream out = new FileOutputStream(dest);
        try {
            byte[] buf = new byte[64 * 1024];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        } finally {
            try { in.close(); } catch (IOException ignored) { }
            out.close();
        }
    }

    private static String queryDisplayName(ContentResolver cr, Uri treeUri, String docId) {
        Uri docUri = DocumentsContract.buildDocumentUriUsingTree(treeUri, docId);
        Cursor c = cr.query(docUri,
                new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME}, null, null, null);
        if (c == null) return null;
        try {
            return c.moveToFirst() ? c.getString(0) : null;
        } finally {
            c.close();
        }
    }

    private static String sanitize(String name) {
        if (name == null) return "";
        return name.replace('/', '_').replace('\\', '_');
    }

    private static void deleteRecursive(File f) {
        if (!f.exists()) return;
        File[] children = f.listFiles();
        if (children != null) for (File ch : children) deleteRecursive(ch);
        f.delete();
    }

    // ===== 永続化権限まわり =====

    private static SharedPreferences prefs(Context ctx) {
        return ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    private static Uri getSavedTreeUri(Context ctx) {
        String s = prefs(ctx).getString(KEY_TREE_URI, null);
        return s == null ? null : Uri.parse(s);
    }

    private static boolean hasPersistedPermission(Context ctx, Uri uri) {
        for (UriPermission p : ctx.getContentResolver().getPersistedUriPermissions()) {
            if (p.getUri().equals(uri) && p.isReadPermission()) return true;
        }
        return false;
    }
}