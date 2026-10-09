package com.MMD4AR.filepicker;


import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;

public class SafFolderPickerActivity extends Activity {

    static final String EXTRA_CALLBACK  = "callback_object";
    static final String EXTRA_DEST_ROOT = "dest_root";
    private static final int REQUEST_PICK = 9001;

    private String callbackObject;
    private String destRoot;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        callbackObject = getIntent().getStringExtra(EXTRA_CALLBACK);
        destRoot = getIntent().getStringExtra(EXTRA_DEST_ROOT);

        // 再生成時に二重起動しない
        if (savedInstanceState != null) return;

        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION
                | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        try {
            startActivityForResult(intent, REQUEST_PICK);
        } catch (Exception e) {
            SafFolderBridge.notifyError(callbackObject, "launch_failed: " + e.getMessage());
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_PICK) { finish(); return; }

        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            SafFolderBridge.notifyCancelled(callbackObject);
            finish();
            return;
        }

        Uri treeUri = data.getData();
        try {
            getContentResolver().takePersistableUriPermission(
                    treeUri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            SafFolderBridge.saveTreeUri(getApplicationContext(), treeUri);
        } catch (Exception ignored) {
            // 永続化できないプロバイダでも、今回のコピーは可能
        }

        SafFolderBridge.startCopy(getApplicationContext(), treeUri, callbackObject, destRoot);
        finish(); // Unityへすぐ戻す。コピーはバックグラウンドで継続
    }
}