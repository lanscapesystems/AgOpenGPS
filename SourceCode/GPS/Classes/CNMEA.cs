﻿using System;
using System.Globalization;
using System.Text;

namespace AgOpenGPS
{
    public class CNMEA
    {
        // turn on functions used with GPS Antenna on tool
        public bool isGPSTool = false;
        public bool isGPSToolOnly = false;
        public double toolAntennaHeight, toolAntennaOffset; 

        //WGS84 Lat Long
        public double latitude, longitude;
        public double latitudeTool, longitudeTool;

        //local plane geometry
        public double latStart, lonStart;
        public double mPerDegreeLat, mPerDegreeLon;

        //our current fix
        public vec2 fix = new vec2(0, 0);
        public vec2 fixTool = new vec2(0, 0);

        //used to offset the antenna position to compensate for drift
        public vec2 fixOffset = new vec2(0, 0);

        //other GIS Info
        public double altitude, speed;

        public double headingTrueDual, headingTrueDualTool, headingTrue, hdop, age, hdopTool, ageTool, headingTrueDualOffset;

        public int fixQuality, fixQualityTool, ageAlarm;
        public int satellitesTracked, satellitesTrackedTool;

        public StringBuilder logNMEASentence = new StringBuilder();

        private readonly FormGPS mf;
        public CNMEA(FormGPS f)
        {
            //constructor, grab the main form reference
            mf = f;
            latStart = 0;
            lonStart = 0;
            ageAlarm = Properties.Settings.Default.setGPS_ageAlarm;
            isGPSTool = Properties.Settings.Default.setGPS_isGPSTool;
            isGPSToolOnly = Properties.Settings.Default.setGPS_isGPSToolOnly;
            toolAntennaHeight = Properties.Vehicle.Default.setTool_antennaHeight;
            toolAntennaOffset = Properties.Vehicle.Default.setTool_antennaOffset;

        }

        public void AverageTheSpeed()
        {
            //average the speed
            //if (mf.isReverse) speed *= -1;

            mf.avgSpeed = (mf.avgSpeed * 0.5) + (speed * 0.5);
        }

        public void SetLocalMetersPerDegree()
        {
            mPerDegreeLat = 111132.92 - 559.82 * Math.Cos(2.0 * latStart * 0.01745329251994329576923690766743) + 1.175
            * Math.Cos(4.0 * latStart * 0.01745329251994329576923690766743) - 0.0023
            * Math.Cos(6.0 * latStart * 0.01745329251994329576923690766743);

            mPerDegreeLon = 111412.84 * Math.Cos(latStart * 0.01745329251994329576923690766743) - 93.5
            * Math.Cos(3.0 * latStart * 0.01745329251994329576923690766743) + 0.118
            * Math.Cos(5.0 * latStart * 0.01745329251994329576923690766743);

            ConvertWGS84ToLocal(latitude, longitude, out double northing, out double easting);
            mf.worldGrid.checkZoomWorldGrid(northing, easting);
        }

        public void ConvertWGS84ToLocal(double Lat, double Lon, out double Northing, out double Easting)
        {
            mPerDegreeLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 
                * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 
                * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);

            Northing = (Lat - latStart) * mPerDegreeLat;
            Easting = (Lon - lonStart) * mPerDegreeLon;

            //Northing += mf.RandomNumber(-0.02, 0.02);
            //Easting += mf.RandomNumber(-0.02, 0.02);
        }

        public void ConvertLocalToWGS84(double Northing, double Easting, out double Lat, out double Lon)
        {
            Lat = ((Northing + fixOffset.northing) / mPerDegreeLat) + latStart;
            mPerDegreeLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);
            Lon = ((Easting + fixOffset.easting) / mPerDegreeLon) + lonStart;
        }

        public string GetLocalToWSG84_KML(double Easting, double Northing)
        {
            double Lat = (Northing / mPerDegreeLat) + latStart;
            mPerDegreeLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);
            double Lon = (Easting / mPerDegreeLon) + lonStart;

            return Lon.ToString("N7", CultureInfo.InvariantCulture) + ',' + Lat.ToString("N7", CultureInfo.InvariantCulture) + ",0 ";
        }
    }
}