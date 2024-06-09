using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace _Project.Scripts
{
    public static class LocationHelper
    {
        private const double EarthRadius = 6371; // Радиус Земли в километрах

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Преобразуем градусы в радианы
            lat1 = ToRadians(lat1);
            lon1 = ToRadians(lon1);
            lat2 = ToRadians(lat2);
            lon2 = ToRadians(lon2);

            // Вычисляем разницу широт и долгот
            var dLat = lat2 - lat1;
            var dLon = lon2 - lon1;

            // Применяем формулу Хаверсина
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            // Возвращаем расстояние в километрах
            return EarthRadius * c;
        }

        public static SavedModel FindNearestModel(double latitude, double longitude, List<SavedModel> models)
        {
            SavedModel nearestElement = null;
            var minDistance = double.MaxValue;

            foreach (var m in models)
            {
                var distanceToElement = CalculateDistance(latitude, longitude, m.latitude, m.longitude);

                if (!(distanceToElement < minDistance)) continue;
                minDistance = distanceToElement;
                nearestElement = m;
            }

            return nearestElement;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
    }

    [Serializable]
    public class SavedModel
    {
        [FormerlySerializedAs("SavedPath")] public string savedPath;
        [FormerlySerializedAs("Latitude")] public double latitude;
        [FormerlySerializedAs("Longitude")] public double longitude;
    }
}