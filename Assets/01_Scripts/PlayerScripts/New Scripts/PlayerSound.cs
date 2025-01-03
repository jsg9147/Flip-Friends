using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class PlayerSound : NetworkBehaviour
{
    public AudioSource jumpSound;
    public AudioSource shirinkSound;
    public AudioSource enterSound;
    public AudioSource exitSound;


    private void Start()
    {
        SetVolume();
    }

    [ClientRpc]
    public void RpcPlayJumpSound()
    {
        jumpSound.Play(); // 여러 클립 재생 가능
    }

    [ClientRpc]
    public void RpcPlayShrinkSound()
    {
        shirinkSound.Play(); // 여러 클립 재생 가능
    }

    [ClientRpc]
    public void RpcPlayEnterSound()
    {
        shirinkSound.Play(); // 여러 클립 재생 가능
    }

    [ClientRpc]
    public void RpcPlayExitSound()
    {
        shirinkSound.Play(); // 여러 클립 재생 가능
    }

    void SetVolume()
    {
        jumpSound.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        shirinkSound.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        enterSound.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        exitSound.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }
}
