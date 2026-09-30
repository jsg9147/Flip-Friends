using UnityEngine;
using System.Collections.Generic;

public class DebugLogDisplay : MonoBehaviour
{
    private List<string> logMessages = new List<string>();
    private Queue<string> messageQueue = new Queue<string>();
    private const int maxMessages = 10;

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string message = logString;

        if (messageQueue.Count >= maxMessages)
        {
            messageQueue.Dequeue();
        }

        messageQueue.Enqueue(message); 
    }

    private void OnGUI()
    {
        GUILayout.BeginVertical("box");

        foreach (string log in messageQueue)
        {
            GUILayout.Label(log);
        }

        GUILayout.EndVertical();
    }
}
