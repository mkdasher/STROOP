﻿using STROOP.Forms;
using STROOP.Structs;
using STROOP.Structs.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STROOP.Utilities
{
    public static class TriangleUtilities
    {
        public static List<TriangleStruct> GetLevelTriangles()
        {
            uint triangleListAddress = Config.Stream.GetUInt32(TriangleConfig.TriangleListPointerAddress);
            int numLevelTriangles = Config.Stream.GetInt32(TriangleConfig.LevelTriangleCountAddress);
            return GetTrianglesInRange(triangleListAddress, numLevelTriangles);
        }

        public static List<uint> GetLevelTriangleAddresses()
        {
            uint triangleListAddress = Config.Stream.GetUInt32(TriangleConfig.TriangleListPointerAddress);
            int numLevelTriangles = Config.Stream.GetInt32(TriangleConfig.LevelTriangleCountAddress);
            return GetTriangleAddressesInRange(triangleListAddress, numLevelTriangles);
        }

        public static List<TriangleStruct> GetObjectTriangles()
        {
            uint triangleListAddress = Config.Stream.GetUInt32(TriangleConfig.TriangleListPointerAddress);
            int numTotalTriangles = Config.Stream.GetInt32(TriangleConfig.TotalTriangleCountAddress);
            int numLevelTriangles = Config.Stream.GetInt32(TriangleConfig.LevelTriangleCountAddress);

            uint objectTriangleListAddress = triangleListAddress + (uint)(numLevelTriangles * TriangleConfig.TriangleStructSize);
            int numObjectTriangles = numTotalTriangles - numLevelTriangles;

            return GetTrianglesInRange(objectTriangleListAddress, numObjectTriangles);
        }

        public static List<TriangleStruct> GetAllTriangles()
        {
            uint triangleListAddress = Config.Stream.GetUInt32(TriangleConfig.TriangleListPointerAddress);
            int numTotalTriangles = Config.Stream.GetInt32(TriangleConfig.TotalTriangleCountAddress);
            return GetTrianglesInRange(triangleListAddress, numTotalTriangles);
        }

        public static List<TriangleStruct> GetTrianglesInRange(uint startAddress, int numTriangles)
        {
            List<TriangleStruct> triangleList = new List<TriangleStruct>();
            for (int i = 0; i < numTriangles; i++)
            {
                uint address = startAddress + (uint)(i * TriangleConfig.TriangleStructSize);
                TriangleStruct triangle = new TriangleStruct(address);
                triangleList.Add(triangle);
            }
            return triangleList;
        }

        public static List<uint> GetTriangleAddressesInRange(uint startAddress, int numTriangles)
        {
            List<uint> triangleAddressList = new List<uint>();
            for (int i = 0; i < numTriangles; i++)
            {
                uint address = startAddress + (uint)(i * TriangleConfig.TriangleStructSize);
                triangleAddressList.Add(address);
            }
            return triangleAddressList;
        }

        public static void ShowTriangles(List<TriangleStruct> triangleList)
        {
            InfoForm infoForm = new InfoForm();
            infoForm.SetTriangles(triangleList);
            infoForm.Show();
        }

        public static void NeutralizeTriangles(TriangleClassification? classification = null)
        {
            List<uint> triangleAddresses = GetLevelTriangleAddresses();
            triangleAddresses.ForEach(address =>
            {
                float ynorm = Config.Stream.GetSingle(address + TriangleOffsetsConfig.NormY);
                TriangleClassification triClassification = CalculateClassification(ynorm);
                if (classification == null || classification == triClassification)
                {
                    ButtonUtilities.NeutralizeTriangle(address);
                }
            });
        }

        public static void DisableCamCollision(TriangleClassification? classification = null)
        {
            List<uint> triangleAddresses = GetLevelTriangleAddresses();
            triangleAddresses.ForEach(address =>
            {
                float ynorm = Config.Stream.GetSingle(address + TriangleOffsetsConfig.NormY);
                TriangleClassification triClassification = CalculateClassification(ynorm);
                if (classification == null || classification == triClassification)
                {
                    ButtonUtilities.DisableCamCollisionForTriangle(address);
                }
            });
        }

        public static TriangleClassification CalculateClassification(double yNorm)
        {
            if (yNorm > 0.01) return TriangleClassification.Floor;
            if (yNorm < -0.01) return TriangleClassification.Ceiling;
            return TriangleClassification.Wall;
        }
    }
} 
