package com.example.filepicker;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.database.Cursor;
import android.provider.OpenableColumns;
import android.content.Context;
import android.net.Uri;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

import com.unity3d.player.UnityPlayer;

public class FilePickerActivity extends Activity {
    private static final int REQUEST_CODE = 1001;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");

        startActivityForResult(intent, REQUEST_CODE);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != REQUEST_CODE) {
            return;
        }

        if (resultCode == RESULT_OK && data != null) {
            Uri uri = data.getData();
            String result = "";
            try {
                result = copyToCache(this, uri, getFileName(this, uri));
            } catch (Exception e) {
                e.printStackTrace();
                result = "";
            }

            if (uri != null) {
                UnityPlayer.UnitySendMessage(
                        "FilePickerReceiver",
                        "OnFileSelected",
                        result);
            }
        }

        finish();
    }

    public static String copyToCache(Context context, Uri uri, String fileName) throws Exception {
        File outFile = new File(context.getCacheDir(), fileName);

        try (InputStream in = context.getContentResolver().openInputStream(uri);
                OutputStream out = new FileOutputStream(outFile)) {
            byte[] buffer = new byte[8192];
            int len;
            while ((len = in.read(buffer)) > 0) {
                out.write(buffer, 0, len);
            }
        }
        return outFile.getAbsolutePath();
    }

    public static String getFileName(Context context, Uri uri) {
        String name = null;

        try (Cursor cursor = context.getContentResolver()
                .query(uri, null, null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                int index = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (index >= 0) {
                    name = cursor.getString(index);
                }
            }
        }

        if (name == null) {
            name = "picked_file"; // 取得できなかった場合の仮名
        }
        return name;
    }
}