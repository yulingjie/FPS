using UnityEngine;
using System;
using UnityEditor;



class TestClient:MonoBehaviour{
    void Start()
    {

    }

    void OnGUI()
    {
        if(GUI.Button(new Rect(50,50,150,100), "Connect"))
        {
            NetworkClient.Instance.Connect();
        }
        if(GUI.Button(new Rect(50,200,150,100),"Send Echo Message"))
        {
            NetworkClient.Instance.SendEchoMessage();
        }
        if(GUI.Button(new Rect(50,400, 150,100), "Disconnect"))
        {
            NetworkClient.Instance.Disconnect();
        }
    }
}
