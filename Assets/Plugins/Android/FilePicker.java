package com.example.filepicker;

import android.app.Activity;
import android.content.Intent;

public class FilePicker
{
    public static void OpenFilePicker(Activity activity)
    {
        Intent intent = new Intent(activity, FilePickerActivity.class);
        activity.startActivity(intent);
    }
}