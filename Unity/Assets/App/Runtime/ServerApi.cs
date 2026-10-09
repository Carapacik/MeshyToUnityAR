using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace StreetArt
{
    public sealed class ServerApi
    {
        public string BaseUrl { get; private set; }
        public ServerApi(string baseUrl) { SetAddress(baseUrl); }
        public void SetAddress(string value)
        {
            if (!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri) ||
                !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Use an HTTPS server URL, or http://localhost:5052 for USB development.");
            BaseUrl = value.Trim().TrimEnd('/');
        }
        public IEnumerator Request<T>(string method,string path,object body,Action<T> success,Action<string> failure)
        {
            using var request = new UnityWebRequest(BaseUrl+path,method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 45;
            if(body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.SetRequestHeader("Content-Type","application/json");
            }
            yield return request.SendWebRequest();
            if(request.result != UnityWebRequest.Result.Success)
            {
                string message = "Cannot reach the server. Check its address and connection.";
                try { var error=JsonUtility.FromJson<ApiError>(request.downloadHandler.text); if(!string.IsNullOrEmpty(error?.message)) message=error.message; } catch {}
                failure(message); yield break;
            }
            T value;
            try { value=JsonUtility.FromJson<T>(request.downloadHandler.text); }
            catch { failure("The server returned an invalid response."); yield break; }
            if(value == null) { failure("The server returned an empty response."); yield break; }
            success(value);
        }
    }
}
