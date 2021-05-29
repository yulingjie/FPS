using UnityEngine;
using System;
using System.Collections.Generic;

class CountdownTimer: MonoBehaviour
{
    class Timer
    {
        public float miliseconds;
        public float miliseconds_rt;
        public Action callback;
        public uint timerId;
    }
    private List<Timer> _timers;

    public static CountdownTimer Instance{
        get{
            return s_Instance;
        }
    }
    private static CountdownTimer s_Instance;
    private static uint timerId;
    
  
    public uint StartTimer(float miliseconds, Action callback)
    {
        Timer timer = new Timer();
        timer.miliseconds = miliseconds;
        timer.miliseconds_rt = miliseconds;
        timer.callback = callback;
        _timers.Add(timer);
        _timers.Sort();
        timerId ++;
        timer.timerId = timerId;
        return timerId;
    }
    public void RestartTimer(uint timerId)
    {
        var timer = _timers.Find((tm) => tm.timerId == timerId);
        if(timer != null)
        {
            timer.miliseconds = timer.miliseconds_rt;
        }
    }
    public void StopTimer(uint timerId)
    {
        var timer = _timers.Find((tm)=> tm.timerId == timerId);
        _timers.Remove(timer);
    }

    void Awake()
    {
        _timers = new List<Timer>();
        s_Instance = this;

    }

    private List<Timer> _toDeleteList = new List<Timer>();
    void FixedUpdate()
    {
        _toDeleteList.Clear();
        float deltaTime = Time.fixedDeltaTime;
        for(int i =0; i < _timers.Count;++i)
        {
            _timers[i].miliseconds_rt -= deltaTime;
            if(_timers[i].miliseconds_rt <= 0.0000001f)
            {
                _toDeleteList.Add(_timers[i]);
            }
        }
        for(int i= 0; i < _toDeleteList.Count; ++i)
        {
            var timer = _toDeleteList[i];
            _timers.Remove(timer);
            if(timer.callback != null)
            {
                timer.callback();
            }
        }
        _toDeleteList.Clear();
    }
}
