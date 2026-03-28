using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.Networking;

public static class UnityWebRequestAsyncOperationExtensions
{
    public static TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
    {
        var tcs = new TaskCompletionSource<UnityWebRequest>();
        asyncOp.completed += _ => tcs.SetResult(asyncOp.webRequest);
        return tcs.Task.GetAwaiter();
    }
}
