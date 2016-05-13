﻿using PlayerInfoLibrary;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ApokPT.RocketPlugins
{
    internal class DestructionProcessing
    {
        internal static List<Destructible> destroyList = new List<Destructible>();
        internal static int dIdx = 0;
        internal static int dIdxCount = 0;
        internal static bool processing = false;

        internal static List<Destructible> cleanupList = new List<Destructible>();
        internal static int cdIdx = 0;
        internal static int cdIdxCount = 0;
        internal static List<object[]> playersListBuildables = new List<object[]>();
        internal static int plbIdx = 0;
        internal static List<object[]> playersListFiles = new List<object[]>();
        internal static int plfIdx = 0;
        internal static bool cleanupProcessingBuildables = false;
        internal static bool cleanupProcessingFiles = false;
        internal static UnturnedPlayer originalCaller = null;
        internal static DateTime lastRunTimeWreck = DateTime.Now;
        internal static DateTime lastGetCleanupInfo = DateTime.Now;

        internal static bool syncError;
        internal static void Wreck(IRocketPlayer caller, string filter, uint radius, Vector3 position, WreckType type, FlagType flagtype, ulong steamID, ushort itemID)
        {
            bool pInfoLibLoaded = false;
            syncError = false;
            if (type == WreckType.Wreck)
            {
                if (DestructionProcessing.processing)
                {
                    UnturnedChat.Say(caller, WreckingBall.Instance.Translate("wreckingball_processing", originalCaller != null ? originalCaller.CharacterName : "???", (dIdxCount - dIdx), CalcProcessTime()));
                    return;
                }
                Abort(WreckType.Wreck);
            }
            else if (type == WreckType.Scan)
            {
                WreckingBall.ElementData.reportLists[BuildableType.Element].Clear();
                WreckingBall.ElementData.reportLists[BuildableType.VehicleElement].Clear();
                if (WreckingBall.Instance.Configuration.Instance.EnablePlayerInfo)
                {
                    pInfoLibLoaded = WreckingBall.IsPInfoLibLoaded();
                }
            }
            UnturnedPlayer Player = null;
            if (!(caller is ConsolePlayer) && type != WreckType.Cleanup)
            {
                Player = (UnturnedPlayer)caller;
                if (Player.IsInVehicle)
                    position = Player.CurrentVehicle.transform.position;
                else
                    position = Player.Position;
            }

            List<char> Filter = new List<char>();
            Filter.AddRange(filter.ToCharArray());

            ushort item = 0;
            float distance = 0;
            byte x;
            byte y;
            ushort plant;

            Transform transform;
            int transformCount = 0;

            StructureRegion structureRegion;
            BarricadeRegion barricadeRegion;
            StructureData sData = null;
            BarricadeData bData = null;
            int DataCount = 0;

            for (int k = 0; k < StructureManager.StructureRegions.GetLength(0); k++)
            {
                for (int l = 0; l < StructureManager.StructureRegions.GetLength(1); l++)
                {
                    // check to see if the region is out of range, skip if it is.
                    if (position.RegionOutOfRange(k, l, radius))
                        continue;

                    structureRegion = StructureManager.StructureRegions[k, l];
                    transformCount = structureRegion.Structures.Count;
                    DataCount = structureRegion.StructureDatas.Count;
                    for (int i = 0; i < transformCount; i++)
                    {
                        transform = structureRegion.Structures[i];
                        if (i < DataCount)
                            sData = structureRegion.StructureDatas[i];
                        else
                        {
                            Logger.LogWarning(WreckingBall.Instance.Translate("wreckingball_structure_array_sync_error"));
                            syncError = true;
                            continue;
                        }
                        distance = Vector3.Distance(transform.position, position);
                        if (distance < radius && type != WreckType.Cleanup)
                        {
                            item = sData.structure.id;
                            if (WreckingBall.ElementData.filterItem(item, Filter) || Filter.Contains('*') || flagtype == FlagType.ItemID)
                            {
                                if (flagtype == FlagType.Normal)
                                    WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.Element, type, sData, bData, transform, 's');
                                else if (flagtype == FlagType.SteamID && sData.owner == steamID)
                                    WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.Element, type, sData, bData, transform, 's');
                                else if (flagtype == FlagType.ItemID && itemID == item)
                                    WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.Element, type, sData, bData, transform, 's');
                            }
                        }
                        else if (type == WreckType.Cleanup)
                        {
                            if (sData.owner == steamID)
                                DestructionProcessing.cleanupList.Add(new Destructible(transform, 's'));
                        }
                    } //
                }
            }

            for (int k = 0; k < BarricadeManager.BarricadeRegions.GetLength(0); k++)
            {
                for (int l = 0; l < BarricadeManager.BarricadeRegions.GetLength(1); l++)
                {
                    // check to see if the region is out of range, skip if it is.
                    if (position.RegionOutOfRange(k, l, radius))
                        continue;

                    barricadeRegion = BarricadeManager.BarricadeRegions[k, l];
                    transformCount = barricadeRegion.drops.Count;
                    DataCount = barricadeRegion.BarricadeDatas.Count;
                    for (int i = 0; i < transformCount; i++)
                    {
                        transform = barricadeRegion.drops[i].model;
                        if (i < DataCount)
                            bData = barricadeRegion.BarricadeDatas[i];
                        else
                        {
                            Logger.LogWarning(WreckingBall.Instance.Translate("wreckingball_barricade_array_sync_error"));
                            syncError = true;
                            continue;
                        }
                        distance = Vector3.Distance(transform.position, position);
                        if (distance < radius && type != WreckType.Cleanup)
                        {
                            item = bData.barricade.id;
                            if (WreckingBall.ElementData.filterItem(item, Filter) || Filter.Contains('*') || flagtype == FlagType.ItemID)
                            {
                                if (flagtype == FlagType.Normal)
                                    WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.Element, type, sData, bData, transform, 'b');
                                else if (flagtype == FlagType.SteamID && bData.owner == steamID)
                                    WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.Element, type, sData, bData, transform, 'b');
                                else if (flagtype == FlagType.ItemID && itemID == item)
                                    WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.Element, type, sData, bData, transform, 'b');
                            }
                        }
                        else if (type == WreckType.Cleanup)
                        {
                            if (bData.owner == steamID)
                                DestructionProcessing.cleanupList.Add(new Destructible(transform, 'b'));
                        }
                    } //

                }
            }


            foreach (InteractableVehicle vehicle in VehicleManager.Vehicles)
            {
                distance = Vector3.Distance(vehicle.transform.position, position);
                if (distance < radius)
                {
                    if (BarricadeManager.tryGetPlant(vehicle.transform, out x, out y, out plant, out barricadeRegion))
                    {
                        transformCount = barricadeRegion.drops.Count;
                        DataCount = barricadeRegion.BarricadeDatas.Count;
                        for (int i = 0; i < transformCount; i++)
                        {
                            transform = barricadeRegion.drops[i].model;
                            if (i < DataCount)
                                bData = barricadeRegion.BarricadeDatas[i];
                            else
                            {
                                Logger.LogWarning(WreckingBall.Instance.Translate("wreckingball_barricade_array_sync_error"));
                                syncError = true;
                                continue;
                            }
                            distance = Vector3.Distance(transform.position, position);
                            if (distance < radius && type != WreckType.Cleanup)
                            {
                                item = bData.barricade.id;
                                if (WreckingBall.ElementData.filterItem(item, Filter) || Filter.Contains('*') || flagtype == FlagType.ItemID)
                                {
                                    if (flagtype == FlagType.Normal)
                                        WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.VehicleElement, type, sData, bData, transform, 'b');
                                    else if (flagtype == FlagType.SteamID && bData.owner == steamID)
                                        WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.VehicleElement, type, sData, bData, transform, 'b');
                                    else if (flagtype == FlagType.ItemID && itemID == item)
                                        WreckProcess(caller, item, distance, pInfoLibLoaded, BuildableType.VehicleElement, type, sData, bData, transform, 'b');
                                }
                            }
                            else if (type == WreckType.Cleanup)
                            {
                                if (bData.owner == steamID)
                                    DestructionProcessing.cleanupList.Add(new Destructible(transform, 'b'));
                            }
                        } //

                    }
                    else
                    {
                        barricadeRegion = null;
                    }
                    if ((Filter.Contains('v') || Filter.Contains('*')) && type != WreckType.Cleanup && flagtype == FlagType.Normal)
                    {
                        if (type == WreckType.Scan)
                        {
                            if (distance <= 10)
                                WreckingBall.ElementData.report(caller, 9999, distance, true, pInfoLibLoaded, BuildableType.Vehicle, (ulong)(barricadeRegion == null ? 0 : barricadeRegion.drops.Count));
                            else
                                WreckingBall.ElementData.report(caller, 9999, distance, false, pInfoLibLoaded, BuildableType.Vehicle);
                        }
                        else
                            DestructionProcessing.destroyList.Add(new Destructible(vehicle.transform, 'v', vehicle));
                    }
                }
            }

            if (Filter.Contains('z'))
            {
                for (int v = 0; v < ZombieManager.ZombieRegions.Length; v++)
                {

                    foreach (Zombie zombie in ZombieManager.ZombieRegions[v].Zombies)
                    {
                        distance = Vector3.Distance(zombie.transform.position, position);
                        if (distance < radius)
                        {
                            if (type == WreckType.Scan)
                                WreckingBall.ElementData.report(caller, 9998, (int)distance, false, pInfoLibLoaded);
                            else
                                DestructionProcessing.destroyList.Add(new Destructible(zombie.transform, 'z', null, zombie));
                        }
                    }
                }
            }


            if (type == WreckType.Scan) return;

            if (DestructionProcessing.destroyList.Count >= 1 && type == WreckType.Wreck)
            {
                DestructionProcessing.dIdxCount = DestructionProcessing.destroyList.Count;
                WreckingBall.Instance.Instruct(caller);
            }
            else if (type == WreckType.Cleanup)
            {
                DestructionProcessing.cdIdxCount = DestructionProcessing.cleanupList.Count;
            }
            else
                UnturnedChat.Say(caller, WreckingBall.Instance.Translate("wreckingball_not_found", radius));
        }

        private static void WreckProcess(IRocketPlayer caller, ushort itemID, float distance, bool pInfoLibLoaded, BuildableType buildType, WreckType type, StructureData sData, BarricadeData bData, Transform transform, char elementType)
        {

            if (type == WreckType.Scan)
            {
                if (distance <= 10)
                    WreckingBall.ElementData.report(caller, itemID, distance, true, pInfoLibLoaded, buildType, elementType == 's' ? sData.owner : bData.owner);
                else
                    WreckingBall.ElementData.report(caller, itemID, distance, false, pInfoLibLoaded, buildType, elementType == 's' ? sData.owner : bData.owner);
            }
            else
            {
                if (elementType == 's')
                    destroyList.Add(new Destructible(transform, 's'));
                else
                    destroyList.Add(new Destructible(transform, 'b'));
            }
        }

        internal static double CalcProcessTime()
        {
            return Math.Round(((dIdxCount - dIdx) * (1 / (WreckingBall.Instance.Configuration.Instance.DestructionRate * WreckingBall.Instance.Configuration.Instance.DestructionsPerInterval))), 2);
        }

        internal static void Abort(WreckType type)
        {
            if (type == WreckType.Wreck)
            {
                processing = false;
                destroyList.Clear();
                dIdx = 0;
                dIdxCount = 0;
                originalCaller = null;
            }
            else
            {
                cleanupProcessingBuildables = false;
                cleanupProcessingFiles = false;
                cleanupList.Clear();
                cdIdx = 0;
                cdIdxCount = 0;
                playersListBuildables.Clear();
                playersListFiles.Clear();
                plbIdx = 0;
                plfIdx = 0;
            }
        }

        internal static void HandleCleanup()
        {
            if (cleanupProcessingBuildables)
            {
                if (cleanupList.Count <= cdIdx)
                {
                    if (cdIdxCount != 0)
                        Logger.Log(WreckingBall.Instance.Translate("wreckingball_complete", cdIdx));
                    cleanupList.Clear();
                    cdIdx = 0;
                    cdIdxCount = 0;
                    try
                    {
                        if (!syncError && WreckingBall.IsPInfoLibLoaded())
                            PlayerInfoLib.Database.SetOption((CSteamID)((ulong)playersListBuildables[plbIdx][0]), OptionType.Buildables, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex);
                    }
                    plbIdx++;
                    if (plbIdx >= WreckingBall.Instance.Configuration.Instance.CleanupPerInterval || plbIdx >= playersListBuildables.Count)
                    {
                        Logger.Log("Finished with cleaning up the player elements in this run.");
                        plbIdx = 0;
                        playersListBuildables.Clear();
                        cleanupProcessingBuildables = false;
                        StructureManager.save();
                        BarricadeManager.save();
                    }
                    else
                    {
                        Wreck(new RocketPlayer("0"), "*", 100000, new Vector3(0, 0, 0), WreckType.Cleanup, FlagType.SteamID, (ulong)playersListBuildables[plbIdx][0], 0);
                        if (cdIdxCount == 0)
                            Logger.Log(string.Format("No elements found for player: {0} [{1}] ({2}).", playersListBuildables[plbIdx][1].ToString(), playersListBuildables[plbIdx][2].ToString(), (ulong)playersListBuildables[plbIdx][0]));
                        else
                            Logger.Log(string.Format("Cleaning up {0} elements for player: {1} [{2}] ({3}).", cdIdxCount, playersListBuildables[plbIdx][1].ToString(), playersListBuildables[plbIdx][2].ToString(), (ulong)playersListBuildables[plbIdx][0]));
                    }
                }
            }
            if (cleanupProcessingFiles)
            {
                object[] pf = playersListFiles[plfIdx];
                bool found = false;
                for (byte i = 0; i < Customization.FREE_CHARACTERS + Customization.PRO_CHARACTERS; i++)
                {
                    try
                    {
                        if (ServerSavedata.folderExists("/Players/" + (ulong)pf[0] + "_" + i))
                        {
                            ServerSavedata.deleteFolder("/Players/" + (ulong)pf[0] + "_" + i);
                            found = true;
                        }
                        if (WreckingBall.IsPInfoLibLoaded())
                            PlayerInfoLib.Database.SetOption((CSteamID)((ulong)playersListFiles[plfIdx][0]), OptionType.PlayerFiles, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex);
                    }
                }
                if (found)
                    Logger.Log(string.Format("Cleaning up player data folders for player: {0} [{1}] ({2}).", pf[1].ToString(), pf[2].ToString(), (ulong)pf[0]));
                else
                    Logger.Log(string.Format("Player data folders for player: {0} [{1}] ({2}) not found.", pf[1].ToString(), pf[2].ToString(), (ulong)pf[0]));
                plfIdx++;
                if (plfIdx >= WreckingBall.Instance.Configuration.Instance.CleanupPerInterval || plfIdx >= playersListFiles.Count)
                {
                    Logger.Log("Finished with cleaning up the player's data files in this run.");
                    plfIdx = 0;
                    playersListFiles.Clear();
                    cleanupProcessingFiles = false;
                }
            }

            if ((DateTime.Now - lastGetCleanupInfo).TotalSeconds > WreckingBall.Instance.Configuration.Instance.CleanupIntervalTime * 60)
            {
                lastGetCleanupInfo = DateTime.Now;
                if (WreckingBall.Instance.Configuration.Instance.BuildableCleanup)
                {
                    if (playersListBuildables.Count == 0 && WreckingBall.IsPInfoLibLoaded())
                    {
                        GetCleanupList(OptionType.Buildables, WreckingBall.Instance.Configuration.Instance.BuildableWaitTime, WreckingBall.Instance.Configuration.Instance.CleanupPerInterval);
                        if (playersListBuildables.Count != 0)
                        {
                            // Start cleanup sequence for the players elements.
                            cleanupProcessingBuildables = true;
                            Wreck(new RocketPlayer("0"), "*", 100000, new Vector3(0, 0, 0), WreckType.Cleanup, FlagType.SteamID, (ulong)playersListBuildables[plbIdx][0], 0);
                            if (cdIdxCount == 0)
                                Logger.Log(string.Format("No elements found for player: {0} [{1}] ({2}).", playersListBuildables[plbIdx][1].ToString(), playersListBuildables[plbIdx][2].ToString(), (ulong)playersListBuildables[plbIdx][0]));
                            else
                                Logger.Log(string.Format("Cleaning up {0} elements for player: {1} [{2}] ({3}).", cdIdxCount, playersListBuildables[plbIdx][1].ToString(), playersListBuildables[plbIdx][2].ToString(), (ulong)playersListBuildables[plbIdx][0]));
                        }
                    }
                }
                if (WreckingBall.Instance.Configuration.Instance.PlayerDataCleanup)
                {
                    if (playersListFiles.Count == 0 && WreckingBall.IsPInfoLibLoaded())
                    {
                        GetCleanupList(OptionType.PlayerFiles, WreckingBall.Instance.Configuration.Instance.PlayerDataWaitTime, WreckingBall.Instance.Configuration.Instance.CleanupPerInterval);
                        if (playersListFiles.Count != 0)
                        {
                            // Start cleanup sequence for the players files.
                            cleanupProcessingFiles = true;
                        }
                    }
                }
            }
        }

        private static void GetCleanupList(OptionType option, float waitTime, byte numberToProcess)
        {
            Logger.Log(string.Format("Getting list of {0} to cleanup.", option == OptionType.Buildables ? "player buildables" : "player files"));
            List<object[]> tmp = new List<object[]>();
            try
            {
                tmp = PlayerInfoLib.Database.GetCleanupList(option, (DateTime.Now.AddSeconds(-(waitTime * 86400)).ToTimeStamp()));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            if (tmp.Count == 0)
            {
                Logger.Log("No records found.");
                return;
            }
            else
            {
                Logger.Log(string.Format("Found {0} records, processing {1} records.", tmp.Count, tmp.Count < numberToProcess ? tmp.Count : numberToProcess));
                if (option == OptionType.Buildables)
                    playersListBuildables = tmp.GetRange(0, tmp.Count < numberToProcess ? tmp.Count : numberToProcess);
                else
                    playersListFiles = tmp.GetRange(0, tmp.Count < numberToProcess ? tmp.Count : numberToProcess);
            }
        }

        internal static void DestructionLoop(WreckType type)
        {
            try
            {
                int i = 0;
                while (((dIdx < dIdxCount && type == WreckType.Wreck) || (cdIdx < cdIdxCount && type == WreckType.Cleanup)) && i < WreckingBall.Instance.Configuration.Instance.DestructionsPerInterval)
                {
                    Destructible element = type == WreckType.Wreck ? destroyList[dIdx] : cleanupList[cdIdx];

                    if (element.Type == 's')
                    {
                        try { StructureManager.damage(element.Transform, element.Transform.position, 65535, 1, false); }
                        catch { }
                    }

                    else if (element.Type == 'b')
                    {
                        try { BarricadeManager.damage(element.Transform, 65535, 1, false); }
                        catch { }
                    }

                    else if (element.Type == 'v')
                    {
                        try { element.Vehicle.askDamage(65535, true); }
                        catch { }
                    }
                    else if (element.Type == 'z')
                    {
                        EPlayerKill pKill;
                        try
                        {
                            for (int j = 0; j < 100 && !element.Zombie.isDead; j++)
                                element.Zombie.askDamage(255, element.Zombie.transform.up, out pKill);
                        }
                        catch { }
                    }
                    if (type == WreckType.Wreck)
                        dIdx++;
                    else
                        cdIdx++;
                    i++;
                }
                if (destroyList.Count == dIdx && type == WreckType.Wreck)
                {
                    if (originalCaller != null)
                        UnturnedChat.Say(originalCaller, WreckingBall.Instance.Translate("wreckingball_complete", dIdx));
                    else
                        Logger.Log(WreckingBall.Instance.Translate("wreckingball_complete", dIdx));
                    StructureManager.save();
                    BarricadeManager.save();
                    Abort(WreckType.Wreck);
                }
            }
            catch
            {
                throw;
            }
        }
    }
}