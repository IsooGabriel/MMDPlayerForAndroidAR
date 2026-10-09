package com.example.shuttersound;

import android.media.MediaActionSound;

public class ShutterSound
{
    private MediaActionSound _sound;

    public ShutterSound()
    {
        _sound = new MediaActionSound();
        _sound.load(MediaActionSound.SHUTTER_CLICK);
    }

    public void play()
    {
        _sound.play(MediaActionSound.SHUTTER_CLICK);
    }

    public void release()
    {
        _sound.release();
    }
}