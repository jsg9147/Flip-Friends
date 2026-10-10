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

    // 점프는 소유자 예측과 서버 시뮬레이션 양쪽에서 일어난다. 소유자는 예측 시점에 바로 듣고,
    // 나머지는 서버 RPC로 듣는다. 클라이언트 예측에서 ClientRpc를 부르면 서버가 없어 오류가 난다.
    public void PlayJumpSound()
    {
        if (isOwned)
            jumpSound.Play();
        if (isServer)
            RpcPlayJumpSound();
    }

    [ClientRpc(includeOwner = false)]
    private void RpcPlayJumpSound()
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
