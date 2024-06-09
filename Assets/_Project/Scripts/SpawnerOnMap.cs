using System.Collections.Generic;
using System.Linq;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Project.Scripts
{
    public class SpawnerOnMap : MonoBehaviour
    {
        [FormerlySerializedAs("_map")] [SerializeField]
        private AbstractMap map;

        [FormerlySerializedAs("_spawnScale")] [SerializeField]
        private float spawnScale = 1f;

        [FormerlySerializedAs("_markerPrefab")] [SerializeField]
        private GameObject markerPrefab;

        private Vector2d[] _locations;

        private List<GameObject> _spawnedObjects;

        private void Start()
        {
            // mock place marks
            var models = new List<SavedModel>
            {
                new SavedModel
                {
                    latitude = 56.630848,
                    longitude = 47.890566
                }
            };
            if (PlayerPrefs.HasKey("saved_models"))
                models = new List<SavedModel>(PlayerPrefs.GetString("saved_models").Split('|')
                    .Select(JsonUtility.FromJson<SavedModel>));
            _locations = new Vector2d[models.Count];
            _spawnedObjects = new List<GameObject>();
            for (var i = 0; i < models.Count; i++)
            {
                Debug.Log($"SavedModel: {models[i].latitude} {models[i].longitude} {models[i].savedPath}");
                _locations[i] = new Vector2d(models[i].latitude, models[i].longitude);
                var instance = Instantiate(markerPrefab);
                instance.transform.localPosition = map.GeoToWorldPosition(_locations[i]);
                instance.transform.localScale = new Vector3(spawnScale, spawnScale, spawnScale);
                _spawnedObjects.Add(instance);
            }
        }

        private void Update()
        {
            var count = _spawnedObjects.Count;
            for (var i = 0; i < count; i++)
            {
                var spawnedObject = _spawnedObjects[i];
                var location = _locations[i];
                spawnedObject.transform.localPosition = map.GeoToWorldPosition(location);
                spawnedObject.transform.localScale = new Vector3(spawnScale, spawnScale, spawnScale);
            }
        }
    }
}