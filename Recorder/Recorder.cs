using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fougerite;
using Fougerite.Permissions;
using UnityEngine;
using Random = System.Random;

namespace Recorder
{
    public class Recorder : Fougerite.Module
    {
        public readonly Dictionary<string, string> Prefabs = new Dictionary<string, string>()
        {
            {"SingleBed", ";deploy_singlebed"},
            {"MetalCeiling", ";struct_metal_ceiling"},
            {"MetalDoorFrame", ";struct_metal_doorframe"},
            {"MetalFoundation", ";struct_metal_foundation"},
            {"MetalPillar", ";struct_metal_pillar"},
            {"MetalRamp", ";struct_metal_ramp"},
            {"MetalStairs", ";struct_metal_stairs"},
            {"MetalWall", ";struct_metal_wall"},
            {"MetalWindowFrame", ";struct_metal_windowframe"},
            {"WoodCeiling", ";struct_wood_ceiling"},
            {"WoodDoorFrame", ";struct_wood_doorway"},
            {"WoodFoundation", ";struct_wood_foundation"},
            {"WoodPillar", ";struct_wood_pillar"},
            {"WoodRamp", ";struct_wood_ramp"},
            {"WoodStairs", ";struct_wood_stairs"},
            {"WoodWall", ";struct_wood_wall"},
            {"WoodWindowFrame", ";struct_wood_windowframe"},
            {"Campfire", ";deploy_camp_bonfire"},
            {"ExplosiveCharge", ";explosive_charge"},
            {"Furnace", ";deploy_furnace"},
            {"LargeWoodSpikeWall", ";deploy_largewoodspikewall"},
            {"WoodBoxLarge", ";deploy_wood_storage_large"},
            {"MetalDoor", ";deploy_metal_door"},
            {"MetalBarsWindow", ";deploy_metalwindowbars"},
            {"RepairBench", ";deploy_repairbench"},
            {"SleepingBagA", ";deploy_camp_sleepingbag"},
            {"SmallStash", ";deploy_small_stash"},
            {"WoodSpikeWall", ";deploy_woodspikewall"},
            {"Barricade_Fence_Deployable", ";deploy_wood_barricade"},
            {"WoodGate", ";deploy_woodgate"},
            {"WoodGateway", ";deploy_woodgateway"},
            {"Wood_Shelter", ";deploy_wood_shelter"},
            {"WoodBox", ";deploy_wood_box"},
            {"WoodenDoor", ";deploy_wood_door"},
            {"Workbench", ";deploy_workbench"},
        };
        
        public override string Name
        {
            get
            {
                return "Recorder";
            }
        }
        
        public override string Author
        {
            get
            {
                return "DreTaX";
            }
        }

        public override Version Version
        {
            get
            {
                return new Version("1.0");
            }
        }

        public override string Description
        {
            get
            {
                return "Recorder plugin!";
            }
        }
        
        public override void Initialize()
        {
            DataStore.GetInstance().Flush("Recorder");
            DataStore.GetInstance().Flush("RecorderInit");
            Hooks.OnCommand += OnCommand;
            Hooks.OnEntityDeployedWithPlacer += OnEntityDeployedWithPlacer;

            if (!Directory.Exists(Path.Combine(ModuleFolder, "Buildings\\")))
            {
                Directory.CreateDirectory(Path.Combine(ModuleFolder, "Buildings\\"));
            }
        }
        
        public override void DeInitialize()
        {
            Hooks.OnCommand -= OnCommand;
            Hooks.OnEntityDeployedWithPlacer -= OnEntityDeployedWithPlacer;
        }

        private void OnEntityDeployedWithPlacer(Fougerite.Player player, Entity e, Fougerite.Player actualplacer)
        {
            if (actualplacer == null || e == null)
            {
                return;
            }
            
            object state = DataStore.GetInstance().Get("Recorder", actualplacer.UID);
            if (state == null)
            {
                return;
            }

            int cpt = DataStore.GetInstance().Count("RecordedData" + actualplacer.UID);
            if (cpt == 0)
            {
                DataStore.GetInstance().Add("RecorderInit", actualplacer.UID, new Vector3(e.X, e.Y, e.Z));
            }

            DataStore.GetInstance().Add("RecordedData" + actualplacer.UID, "Part" + cpt, e);
        }

        private void OnCommand(Fougerite.Player player, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "record":
                {
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "recorder.record"))
                    {
                        if (args.Length == 0)
                        {
                            player.MessageFrom("Recorder", "Usage: /record name");
                            return;
                        }

                        object state = DataStore.GetInstance().Get("Recorder", player.UID);
                        if (state != null)
                        {
                            player.MessageFrom("Recorder", "Already Recording!");
                            return;
                        }

                        string name = string.Join("_", args).ToLower();
                        DataStore.GetInstance().Flush("RecordedData" + player.UID);
                        DataStore.GetInstance().Add("Recorder", player.UID, 1);
                        DataStore.GetInstance().Add("Recorder_Name", player.UID, name);
                        player.MessageFrom("Recorder", "Recording " + name + ".ini (/rstop to finish!)");
                    }

                    break;
                }
                case "rspawn":
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "recorder.rspawn"))
                    {
                        if (args.Length != 2)
                        {
                            player.MessageFrom("Recorder", "Usage: /rspawn name.ini distance");
                            return;
                        }

                        float distance = 0f;
                        bool success = float.TryParse(args[1], out distance);
                        if (!success)
                        {
                            player.MessageFrom("Recorder", "Usage: /rspawn name.ini distance");
                            return;
                        }

                        if (!File.Exists(Path.Combine(ModuleFolder, "Buildings\\" + args[0])))
                        {
                            player.MessageFrom("Recorder", "Building not found!");
                            return;
                        }
                        
                        IniParser file = new IniParser(Path.Combine(ModuleFolder, "Buildings\\" + args[0]));

                        DataStore.GetInstance().Flush("SpawnedData" + player.UID);
                        DataStore.GetInstance().Remove("RecorderSMs", player.UID);

                        Vector3 playerFront = Util.GetUtil().Infront(player, distance);
                        float groundposition = World.GetWorld().GetGround(playerFront.x, playerFront.z);
                        //playerFront.y = World.GetGround(playerFront.x, playerFront.z);

                        List<string> sections = file.Sections.Where(x => x != "Init").ToList();

                        StructureMaster master = null;
                        int failures = 0;
                        for (int i = 0; i < sections.Count; i++)
                        {
                            Vector3 entPos = Util.GetUtil().CreateVector(float.Parse(file.GetSetting("Part" + i, "PosX")),
                                float.Parse(file.GetSetting("Part" + i, "PosY")),
                                float.Parse(file.GetSetting("Part" + i, "PosZ")));


                            Vector3 spawnPos = Util.GetUtil().CreateVector(entPos.x + playerFront.x, entPos.y + groundposition,
                                entPos.z + playerFront.z);

                            Quaternion spawnRot = Util.GetUtil().CreateQuat(float.Parse(file.GetSetting("Part" + i, "RotX")),
                                float.Parse(file.GetSetting("Part" + i, "RotY")),
                                float.Parse(file.GetSetting("Part" + i, "RotZ")),
                                float.Parse(file.GetSetting("Part" + i, "RotW")));


                            if (master == null)
                            {
                                master = World.GetWorld()
                                    .CreateSM(player, spawnPos.x, spawnPos.y, spawnPos.z, spawnRot);
                            }

                            Entity go = null;
                            try
                            {
                                go = World.GetWorld().SpawnEntity(file.GetSetting("Part" + i, "Prefab"), spawnPos,
                                    spawnRot);
                            }
                            catch
                            {
                                failures++;
                                // Ignore.
                                continue;
                            }

                            try
                            {
                                if (go.IsLootableObject())
                                {
                                    LootableObject lootableObject = (LootableObject) go.Object;
                                    go.ChangeOwner(player);
                                    DataStore.GetInstance().Add("SpawnedData" + player.UID, "Part" + i,
                                        lootableObject.gameObject);
                                }
                                else if (go.IsDeployableObject())
                                {
                                    go.ChangeOwner(player);
                                    DeployableObject deployableObject = (DeployableObject) go.Object;

                                    DataStore.GetInstance().Add("SpawnedData" + player.UID, "Part" + i,
                                        deployableObject.gameObject);
                                }
                                else if (go.IsBasicDoor())
                                {
                                    BasicDoor door = (BasicDoor) go.Object;
                                    go.ChangeOwner(player);
                                    DataStore.GetInstance().Add("SpawnedData" + player.UID, "Part",
                                        door.gameObject);
                                }
                                else if (go.IsStructure())
                                {
                                    StructureComponent structureComponent = (StructureComponent) go.Object;

                                    master.AddStructureComponent(structureComponent);
                                    DataStore.GetInstance().Add("SpawnedData" + player.UID, "Part" + i,
                                        structureComponent.gameObject);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("[Recorder] Error: " + ex);
                                Logger.LogError("At prefab: " + file.GetSetting("Part" + i, "Prefab"));
                            }
                        }

                        player.MessageFrom("Recorder", args[0] + " was spawned !");
                        if (failures > 0)
                        {
                            player.MessageFrom("Recorder", failures + " amount of failures happened. This file might have wrong values!");
                        }
                    }
                    break;
                case "rlist":
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "recorder.rlist"))
                    {
                        string[] files = Directory.GetFiles(Path.Combine(ModuleFolder, "Buildings\\"), "*.ini");
                        
                        for (int i = 0; i < files.Length; i++)
                        {
                            files[i] = Path.GetFileName(files[i]);
                        }

                        player.MessageFrom("Recorder", "=== Files ===");
                        player.MessageFrom("Recorder", string.Join(", ", files));
                    }

                    break;
                case "rcancel":
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "recorder.rcancel"))
                    {
                        int cpt = DataStore.GetInstance().Count("SpawnedData" + player.UID);
                        for (int i = 0; i < cpt; i++)
                        {
                            GameObject ent = DataStore.GetInstance().Get("SpawnedData" + player.UID, "Part" + i) as GameObject;
                            if (ent != null)
                            {
                                Util.GetUtil().DestroyObject(ent);
                            }
                            DataStore.GetInstance().Remove("Recorder", player.UID);
                            DataStore.GetInstance().Flush("RecordedData" + player.UID);
                            DataStore.GetInstance().Flush("SpawnedData" + player.UID);
                            DataStore.GetInstance().Remove("RecorderInit", player.UID);
                            DataStore.GetInstance().Remove("RecorderSMs", player.UID);
                        }

                        DataStore.GetInstance().Flush("SpawnedData" + player.UID);
                        player.Message("Cancelled recording");
                    }
                    break;
                case "rstop":
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "recorder.rstop"))
                    {
                        string name = (string) DataStore.GetInstance().Get("Recorder_Name", player.UID);
                        if (File.Exists(Path.Combine(ModuleFolder, "Buildings\\" + name)))
                        {
                            var rnd = new Random();
                            int rnd2 = rnd.Next(0, 500000);
                            player.Message(name + ".ini already exists ! renaming..");
                            name = name + rnd2;
                        }

                        name = name + ".ini";

                        Vector3 loc = (Vector3) DataStore.GetInstance().Get("RecorderInit", player.UID);
                        float groundposition = World.GetWorld().GetGround(loc.x, loc.z);
                        //loc.y = World.GetGround(loc.x, loc.z)

                        int cpt = DataStore.GetInstance().Count("RecordedData" + player.UID);
                        if (cpt == 0)
                        {
                            DataStore.GetInstance().Flush("RecordedData" + player.UID);
                            return;
                        }
                        
                        if (!File.Exists(Path.Combine(ModuleFolder, "Buildings\\" + name)))
                        {
                            File.Create(Path.Combine(ModuleFolder, "Buildings\\" + name)).Dispose();
                        }
                        IniParser rfile = new IniParser(Path.Combine(ModuleFolder, "Buildings\\" + name));

                        for (int i = 0; i < cpt; i++)
                        {
                            Entity ent = (Entity) DataStore.GetInstance().Get("RecordedData" + player.UID, "Part" + i);
                            Vector3 entPos = new Vector3((ent.X - loc.x), (ent.Y - groundposition),
                                (ent.Z - loc.z));
                            Quaternion spawnRot = ent.Rotation;

                            rfile.AddSetting("Part" + i, "Prefab", Prefabs[ent.Name]);
                            rfile.AddSetting("Part" + i, "PosX", entPos.x.ToString(CultureInfo.CurrentCulture));
                            rfile.AddSetting("Part" + i, "PosY", entPos.y.ToString(CultureInfo.CurrentCulture));
                            rfile.AddSetting("Part" + i, "PosZ", entPos.z.ToString(CultureInfo.CurrentCulture));
                            rfile.AddSetting("Part" + i, "RotX", spawnRot.x.ToString(CultureInfo.CurrentCulture));
                            rfile.AddSetting("Part" + i, "RotY", spawnRot.y.ToString(CultureInfo.CurrentCulture));
                            rfile.AddSetting("Part" + i, "RotZ", spawnRot.z.ToString(CultureInfo.CurrentCulture));
                            rfile.AddSetting("Part" + i, "RotW", spawnRot.w.ToString(CultureInfo.CurrentCulture));
                        }
                        
                        DataStore.GetInstance().Remove("Recorder", player.UID);
                        DataStore.GetInstance().Flush("RecordedData" + player.UID);
                        DataStore.GetInstance().Flush("SpawnedData" + player.UID);
                        DataStore.GetInstance().Remove("RecorderInit", player.UID);
                        DataStore.GetInstance().Remove("RecorderSMs", player.UID);
                        DataStore.GetInstance().Flush("SpawnedData" + player.UID);
                        rfile.Save();
                        player.MessageFrom("Recorder", name + " was saved !");
                    }
                    break;
            }
        }
    }
}