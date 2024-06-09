using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARLocation;
using Siccity.GLTFUtility;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _Project.Scripts
{
    public class SendRequest : MonoBehaviour
    {
        [FormerlySerializedAs("DialogObject")] public GameObject dialogObject;
        [FormerlySerializedAs("InputField")] public TMP_InputField inputField;

        [FormerlySerializedAs("LoadingObject")]
        public GameObject loadingObject;

        [FormerlySerializedAs("LoadingText")] public Text loadingText;

        [FormerlySerializedAs("PublishObject")]
        public GameObject publishObject;

        [FormerlySerializedAs("OtherUIObject")]
        public GameObject otherUIObject;

        public bool isLike;

        private bool _loading;
        private string _resultId = "";

        private void Start()
        {
            if (!isLike) return;

            var models = new List<SavedModel>();
            if (PlayerPrefs.HasKey("saved_models"))
                models = new List<SavedModel>(PlayerPrefs.GetString("saved_models").Split('|')
                    .Select(JsonUtility.FromJson<SavedModel>));
            if (models.Count == 0) return;

            var currentLocation = ARLocationProvider.Instance.Provider.CurrentLocation;
            var nearestModel =
                LocationHelper.FindNearestModel(currentLocation.latitude, currentLocation.longitude, models);
            Debug.Log($"{nearestModel.latitude} {nearestModel.longitude}  {nearestModel.savedPath}");
            var file = File.ReadAllBytes(nearestModel.savedPath);
            var arObject = Importer.LoadFromBytes(file);
            var scale = arObject.transform.localScale;
            arObject.transform.localScale = new Vector3(scale.x * 0.5f, scale.y * 0.5f, scale.z * 0.5f);
            var newLocation = new Location
            {
                Latitude = nearestModel.latitude,
                Longitude = nearestModel.longitude
            };
            PlaceAtLocation.CreatePlacedInstance(arObject, newLocation, new PlaceAtLocation.PlaceAtOptions());
        }

        private void Update()
        {
            if (isLike) return;

            loadingObject.SetActive(_loading);
            if (_loading) otherUIObject.SetActive(false);
        }

        public void OpenGenerateDialog()
        {
            dialogObject.SetActive(true);
        }

        public void HidePublish()
        {
            otherUIObject.SetActive(true);
            publishObject.SetActive(false);
        }

        public void SendPrompt()
        {
            _loading = true;
            StartCoroutine(GetResultId(inputField.textComponent.text));
        }

        private IEnumerator<object> GetResultId(string prompt)
        {
            using var www =
                UnityWebRequest.Get(
                    new Uri($"https://backend.example.com/api/Generation/text-2-result?prompt={prompt}"));
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) Debug.Log(www.error);
            _resultId = www.downloadHandler.text;
            loadingText.text = "Генерация модели...";
            Debug.Log($"resultId = {_resultId}");
            if (!string.IsNullOrEmpty(_resultId) && _loading) StartCoroutine(GetResultById());
        } // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator<object> GetResultById()
        {
            while (_loading)
            {
                using var www =
                    UnityWebRequest.Get(new Uri(
                        $"https://backend.example.com/api/Generation/result-2-model?resultId={_resultId}"));
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(www.error);
                    _loading = false;
                }

                var response = JsonUtility.FromJson<GeneratedContent>(www.downloadHandler.text);
                if (response.progress < 100 && string.IsNullOrEmpty(response.modelUrls.glb))
                {
                    Debug.Log($"progress {response.progress}");
                    yield return new WaitForSeconds(2);
                }
                else
                {
                    loadingText.text = "Загрузка модели...";
                    using var modelWww = UnityWebRequest.Get(response.modelUrls.glb);
                    yield return modelWww.SendWebRequest();
                    if (modelWww.result != UnityWebRequest.Result.Success)
                    {
                        Debug.Log(modelWww.error);
                        _loading = false;
                    }

                    var savedPath = $"{Application.persistentDataPath}/{_resultId}.glb";
                    Debug.Log(savedPath);
                    File.WriteAllBytes(savedPath, modelWww.downloadHandler.data);
                    var models = new List<SavedModel>();
                    var currentLocation = ARLocationProvider.Instance.Provider.CurrentLocation;
                    if (PlayerPrefs.HasKey("saved_models"))
                        models = new List<SavedModel>(PlayerPrefs.GetString("saved_models").Split('|')
                            .Select(JsonUtility.FromJson<SavedModel>));
                    var savedModel = new SavedModel
                    {
                        savedPath = savedPath,
                        latitude = currentLocation.latitude,
                        longitude = currentLocation.longitude
                    };
                    models.Add(savedModel);
                    var list = string.Join("|", models.Select(JsonUtility.ToJson));
                    PlayerPrefs.SetString("saved_models", list);
                    PlayerPrefs.Save();

                    loadingText.text = "Отображение модели...";
                    var importedModel = Importer.LoadFromBytes(modelWww.downloadHandler.data);
                    var scale = importedModel.transform.localScale;
                    importedModel.transform.localScale = new Vector3(scale.x * 0.5f, scale.y * 0.5f, scale.z * 0.5f);
                    var newLocation = new Location
                    {
                        Latitude = savedModel.latitude,
                        Longitude = savedModel.longitude
                    };
                    PlaceAtLocation.CreatePlacedInstance(importedModel, newLocation,
                        new PlaceAtLocation.PlaceAtOptions());

                    inputField.SetTextWithoutNotify("");
                    _loading = false;
                    dialogObject.SetActive(false);
                    otherUIObject.SetActive(false);
                    publishObject.SetActive(true);
                    yield break;
                }
            }
        }
    }


    [Serializable]
    public class GeneratedContent
    {
        public int progress;
        [FormerlySerializedAs("video_url")] public string videoURL;
        [FormerlySerializedAs("model_urls")] public ModelUrls modelUrls;
    }

    [Serializable]
    public class ModelUrls
    {
        public string glb;
    }
}