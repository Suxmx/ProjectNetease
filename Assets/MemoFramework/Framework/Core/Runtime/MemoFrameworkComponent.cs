using System;
using UnityEngine;

namespace MemoFramework
{
    public class MemoFrameworkComponent : MonoBehaviour
    {
        protected virtual void Awake()
        {
            MemoFrameworkEntry.RegisterComponent(this);
        }

    }
}