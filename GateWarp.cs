using MioGame;
using MioModdingApi;
using MioModLoader;
using System.Runtime.CompilerServices;

namespace GateWarp
{
    public class GateWarp : Mod
    {

        private static Dictionary<string, string> gateNames = [];
        private static Dictionary<string, Vec_float_2> gatePositions = [];

        public override void Initialize()
        {
            Hooks();

            ReloadData();

            ConnectMenus();
        }

        public void ReloadData()
        {
            using (StreamReader file = System.IO.File.OpenText(Path.Combine(this.GetModFolderPath(), "warps.dat")))
            {
                string asText = file.ReadToEnd();
                foreach (string line in asText.Split("\n"))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split(":");
                    string id = parts[0];
                    string name = parts[1];
                    gateNames[id] = name;
                    Vec_float_2 pos = VectFromString(parts[2]);
                    gatePositions[id] = pos;
                }
            }
        }


        private static ModMenuPage? modPage;
        private static ChoiceOption? warpLocation;
        private static ToggleOption? unlockNecessity;
        private static ToggleOption? overseerNecessity;
        private static ChoiceOption? wheelBehavior;
        public void ConnectMenus()
        {
            modPage = ModMenu.RegisterMod(this, "GateWarp", "");
            modPage.RegisterButton("warp", "Warp", Warp);
            IReadOnlyList<string> locs = gateNames.Values.ToList<string>();
            warpLocation = modPage.RegisterChoice("warp_loc", "Warp Location", locs, 0, 
                "The location to warp to");
            unlockNecessity = modPage.RegisterToggle("need_unlock", "Visit Necessity", false,
                "Wether you need to have visited the checkpoint in order to warp.");
            overseerNecessity = modPage.RegisterToggle("need_overseer", "Overseer Necessity", false,
                "Wether you need the overseer for the checkpoint in order to warp.");
            wheelBehavior = modPage.RegisterChoice("wheel_behavior", "Wheel Behavior", ["Disallow", "Reset", "Allow"], 1,
                "How teleporting between the halves of the vessel is handled; Reset resets the wheel on teleport.");
        }

        private unsafe void Hooks()
        {
            On.MioGame.On_Game.fixed_update.Prefix += Fixed_update_Prefix;
        }


        private bool queuedWarp = false;

        private unsafe void Fixed_update_Prefix(MioGame.Game* __this)
        {
            if (!queuedWarp) return;
            try
            {
                ref var mio = ref __this->mio;
                ref var save = ref __this->save;
                if ((unlockNecessity.Value && !HasSaveEntry("CHECKPOINT:" + GetSelected(), ref save)) || 
                    (overseerNecessity.Value && !HasSaveEntry("OVERSEER:" + GetSelected(), ref save)))
                {
                    queuedWarp = false;
                    return;
                }
                if (mio.cutscene.active || mio.checkpoint.state._value != Mio.Checkpoint.State.None) return;
                ref var cam = ref __this->game_cam;
                Vec_float_3 pos = GetWarpPosition();

                if (DifferentHalves(cam.position, pos))
                {
                    switch (wheelBehavior.SelectedIndex)
                    {
                        case 0: // Disallowed
                            queuedWarp = false;
                            return;
                        case 1: // Reset Wheel
                            save.halyn_align.configuration_discrete = 2;
                            break;
                        case 2: // Allowed
                            break;
                    }
                }

                mio.move_to(pos);
                cam.position = pos;
                cam.position.z = 35f;
                queuedWarp = false;
            }
            catch (Exception ex)
            {
                LogMessage("Could not warp: " + ex);
            }
        }

        public void Warp()
        {
            queuedWarp = true;
        }

        private static string GetSelected()
        {
            if (warpLocation == null) return "";
            return gateNames.FirstOrDefault(x => x.Value == warpLocation.Value).Key;
        }

        private static Vec_float_3 GetWarpPosition()
        {
            if (warpLocation == null) return new Vec_float_3();
            return GetWorldPos(gatePositions[GetSelected()]);
        }


        private unsafe bool HasSaveEntry(string entry, ref MioGame.Save save)
        {
            MioGame.String mioString = Util.StringToMioString(entry);
            return Unsafe.AsRef<Hashmap_String_Save_entry.Slot>(save.entries.lookup(&mioString, false)).value.count > 0;
        }


        private static Vec_float_2 VectFromString(string str)
        {
            str = str.Substring(1, str.Length - 2);
            string[] parts = str.Split(", ");
            Vec_float_2 result = new()
            {
                x = float.Parse(parts[0]),
                y = float.Parse(parts[1])
            };
            return result;
        }

        private static Vec_float_3 GetWorldPos(Vec_float_2 coords)
        {
            Vec_float_3 result = new()
            {
                x = coords.x,
                y = coords.y
            };
            return result;
        }

        private static bool IsLowerHalf(Vec_float_3 pos)
        {
            return IsLowerHalf(pos.y);
        }
        private static bool IsLowerHalf(float y)
        {
            return y < 1087;
        }
        private static bool DifferentHalves(Vec_float_3 pos1, Vec_float_3 pos2)
        {
            return DifferentHalves(pos1.y, pos2.y);
        }
        private static bool DifferentHalves(float y1, float y2)
        {
            return IsLowerHalf(y1) == IsLowerHalf(y2);
        }
    }
}
