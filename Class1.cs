using System;
using UnityEngine;
using HarmonyLib;
using HarmonyLib.Tools;
using DataStruct.Encount;
using System.IO;
using System.Collections.Generic;

namespace RSaga3Mod
{
    [Serializable]
    public class Settings
    {
        static Settings()
        {
            LoadSettings();
        }

        public static string FilePath => Application.persistentDataPath + "/__modsettings.json";


        public static void LoadSettings()
        {
            if (System.IO.File.Exists(FilePath))
            {
                instance = UnityEngine.JsonUtility.FromJson<Settings>(System.IO.File.ReadAllText(FilePath));
                return;
            }

            instance = new Settings();
            WriteSettings();
        }
        public static void WriteSettings()
        {
            System.IO.File.WriteAllText(FilePath, UnityEngine.JsonUtility.ToJson(instance, true));
        }

        private static int sanitizeSpeed(int fps) => fps < 1 ? 30 : fps;

        public static int GetGameSpeedByIndex(int idx) =>
            sanitizeSpeed(idx switch
            {
                0 => instance.normalFps,
                1 => instance.fastFps,
                2 => instance.turboFps,
                _ => instance.normalFps,
            });

        public static Settings instance = new Settings();

        public bool skipLogos = true;

        public int normalFps = 30;
        public int fastFps = 60;
        public int turboFps = 90;

        public int battleSpeed = 0;
        public int fieldSpeed = 0;

        public int enTextSpeed = 1;
        public int jpTextSpeed = 2;

        public bool mapAnywhere = false;
        public int fastGrow = 0;
        public int fastGrowSkill = 0;
        public int fastGrowEnemy = 0;
        public float sparkRateMultiplier = 1.0f;
        public float acquireRateMultiplier = 1.0f;

        public bool speedrun = false;
    }

    [HarmonyPatch(typeof(GameCore), "Update")]
    public static class TrackGameStateChanges
    {
        public static bool IgnoreNextStateChange { get; set; } = false;

        public static void SetGameSpeedByState(GameCore.State state) =>
            Application.targetFrameRate = state switch
            {
                GameCore.State.BATTLE => Settings.GetGameSpeedByIndex(Settings.instance.battleSpeed),
                GameCore.State.FIELD => Settings.GetGameSpeedByIndex(Settings.instance.fieldSpeed),
                _ => 30
            };

        public static void IncrementCurrentGameStateSpeed()
        {
            if (Settings.instance.speedrun)
                return;
            GameCore.State s = GameCore.m_state;
            if (s == GameCore.State.BATTLE)
            {
                Settings.instance.battleSpeed = (Settings.instance.battleSpeed + 1) % 3;
                Settings.WriteSettings();

                SetGameSpeedByState(s);
            }
            else if (s == GameCore.State.FIELD)
            {
                Settings.instance.fieldSpeed = (Settings.instance.fieldSpeed + 1) % 3;
                Settings.WriteSettings();

                SetGameSpeedByState(s);
            }
        }

        public static GameCore.State prevState;

        public static void Prefix(GameCore __instance)
        {
            prevState = GameCore.m_state;
        }

        public static void Postfix(GameCore __instance)
        {
            if (Settings.instance.speedrun)
                return;
            GameCore.State currState = GameCore.m_state;

            if (prevState != currState && !IgnoreNextStateChange)
            {
                //System.IO.File.AppendAllText("test.txt", $"Detected state change {{{oldState} => {newState}}}\n");
                SetGameSpeedByState(currState);
            }

            IgnoreNextStateChange = false;
        }
    }

    [HarmonyPatch(typeof(GameMain), "Update")]
    public static class SpeedOptions
    {
        static GameObject gui = null;
        public static void Prefix()
        {
            if (Settings.instance.speedrun)
                return;
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                TrackGameStateChanges.IncrementCurrentGameStateSpeed();
            }
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (!gui)
                {
                    gui = new GameObject();
                    gui.AddComponent<SeadCategoryGUIController>();
                }
                else
                {
                    GameObject.Destroy(gui);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SeadCategoryGUIController), "OnGUI")]
    public static class GUIButtons
    {
        static int charselect = 0;
        static int slotselect = 0;
        public static string message = "";
        public static void Postfix()
        {
            GUI.Label(new Rect(8, 70, 96f, 32f), charselect.ToString() + ": " + PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[charselect]._name);
            if (GUI.Button(new Rect(8, 88, 96f, 32f), "-")){
                charselect = Mathf.Max(charselect - 1, 0);
                GameCore.m_partyWork.overwrite_player_at_player_id(charselect, slotselect, slotselect);
                GameCore.m_partyWork._party_number = Mathf.Max(slotselect + 1, GameCore.m_partyWork._party_number);
            }
            if (GUI.Button(new Rect(108, 88, 96f, 32f), "+"))
            {
                charselect = Mathf.Min(charselect + 1, 41);
                GameCore.m_partyWork.overwrite_player_at_player_id(charselect, slotselect, slotselect);
                GameCore.m_partyWork._party_number = Mathf.Max(slotselect + 1, GameCore.m_partyWork._party_number);
            }
            GUI.Label(new Rect(8, 110, 32f, 32f), slotselect.ToString());
            if (GUI.Button(new Rect(8, 128, 96f, 32f), "-"))
            {
                slotselect = Mathf.Max(slotselect - 1, 0);
            }
            if (GUI.Button(new Rect(108, 128, 96f, 32f), "+"))
            {
                slotselect = Mathf.Min(slotselect + 1, 5);
            }
            GUI.Label(new Rect(8, 340, 200f, 64f), message);
            Randomizer.seed = GUI.TextField(new Rect(8, 260, 96f, 32f), Randomizer.seed);
            if(GUI.Button(new Rect(8, 300, 96f, 32f), "Randomize"))
            {
                if (!Randomizer.randomized)
                {
                    Type staticClassInfo = typeof(DataTable);
                    var staticClassConstructorInfo = staticClassInfo.TypeInitializer;
                    staticClassConstructorInfo.Invoke(null, null);
                }
                try
                {
                    string mapdata = File.ReadAllText("mapdata.json");
                    string[] sep = new string[2] { "{\n", "}\n" };
                    string[] mdata = mapdata.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                    string key = "";
                    foreach (string s in mdata)
                    {
                        if (s.Contains(" - "))
                        {
                            key = s.Substring(0, s.IndexOf(" - "));
                            ChestRandomizer.floors[key] = new List<MapInfo.TBox>();
                        }
                        else if(s.Length > 2)
                        {
                            MapInfo.TBox box = JsonUtility.FromJson<MapInfo.TBox>("{\n" + s + "\n}");
                            ChestRandomizer.floors[key].Add(box);
                            ChestRandomizer.boxes.Add(box);
                        }
                    }
                    foreach (string k in ChestRandomizer.floors.Keys)
                    {
                        for (int i = 0; i < ChestRandomizer.floors[k].Count; i++)
                        {
                            int r = Randomizer.rng.Next(ChestRandomizer.boxes.Count);
                            MapInfo.TBox randombox = ChestRandomizer.boxes[r];
                            ChestRandomizer.boxes.RemoveAt(r);
                            ChestRandomizer.floors[k][i].m_flag = randombox.m_flag;
                            ChestRandomizer.floors[k][i].m_gid = randombox.m_gid;
                            ChestRandomizer.floors[k][i].m_tflag = randombox.m_tflag;
                            ChestRandomizer.floors[k][i].m_val = randombox.m_val;
                        }
                    }
                }
                catch (Exception e)
                {
                    message = e.Message;
                }
            }
            if (GUI.Button(new Rect(108, 300, 96f, 32f), "SaveMapData"))
            {
                foreach (BgMap.FloorInfo floorInfo in BgMap.m_floor_list)
                {
                    GameCore.m_field.m_bg.Load("map/" + floorInfo.data_name, -1);
                    GameCore.m_field.m_bg.LoadAttr("map/" + floorInfo.data_name);
                    if (floorInfo.org_id != -1)
                    {
                        MapInfo minfo = MapInfo.GetMapInfo((int)floorInfo.org_id);
                        if (minfo.m_tbox != null)
                        {
                            byte[] utf8Bytes = new byte[floorInfo.disp_name.Length];
                            for (int i = 0; i < floorInfo.disp_name.Length; ++i)
                            {
                                utf8Bytes[i] = (byte)floorInfo.disp_name[i];
                            }
                            File.AppendAllText("mapdata.json", floorInfo.data_name + " - " + System.Text.Encoding.UTF8.GetString(utf8Bytes, 0, utf8Bytes.Length) + "\n");
                            
                            foreach (MapInfo.TBox tbox in minfo.m_tbox)
                                File.AppendAllText("mapdata.json", JsonUtility.ToJson(tbox, true) + "\n");
                        }
                    }
                }
            }
        }
    }

    //[HarmonyPatch(typeof(Field), "GlobalMapJump")]
    //public static class OtherMainBGM
    //{
    //    public static void Postfix(int globalID, Field __instance)
    //    {
    //        int num = globalID * 4;
    //        int no = (int)Data.glb_jump[num];
    //        int dir = (int)Data.glb_jump[num + 1];
    //        int x = (int)Data.glb_jump[num + 2];
    //        int y = (int)Data.glb_jump[num + 3];
    //        int num2 = (no & 65535) >> 10;
    //        int id = __instance.m_player.m_pic_no;
    //        if (num2 == 19)
    //            SoundManager.PlayBGM(11);
    //    }
    //}


    //[HarmonyPatch(typeof(SoundManager), "PlayBGM")]
    //public static class OtherMainBGM
    //{
    //    public static void Postfix(int no)
    //    {
    //        int id = GameCore.m_field.m_player.m_pic_no-8;
    //        if (no >= 19 && no <= 26 && id >= 0)
    //            SoundManager.PlayBGM(id >= 19 && id <= 26 ? id+8 : id);
    //    }
    //}

    [HarmonyPatch(typeof(BattleWork), "info_reset")]
    public static class SparkRate
    {
        public static void Postfix()
        {
            if (Settings.instance.speedrun)
                return;
            BattleWork.hirameki_count = (int)(-8 * (Settings.instance.sparkRateMultiplier - 1.0f));
        }
    }

    [HarmonyPatch(typeof(BattleLogic.GokuiCalc), "is_got_gokui")]
    public static class AcquireRate
    {
        public static void Postfix(ref int use_waza_pt, ref bool gokui_well, ref bool __result)
        {
            if (Settings.instance.speedrun)
                return;
            int num = 30 - 7 * use_waza_pt / 4;
            num = gokui_well ? num * 3 / 2 : num;
            __result = num * Settings.instance.acquireRateMultiplier >= Sys.Rand(0,256);
        }
    }

    [HarmonyPatch(typeof(Field), "Update")]
    public static class MapAnywhere
    {
        public static void Prefix(Field __instance, ref int ___m_next_map_no, ref int ___m_next_map_x, ref int ___m_next_map_y, ref int ___m_next_map_dir)
        {
            if (Settings.instance.speedrun)
                return;
            if (Settings.instance.mapAnywhere && __instance.m_uifield != null && __instance.m_map_info.m_exit_jump == 0)
            {
                __instance.m_uifield.SetVisibleWorldButton(true);
                __instance.m_map_info.m_exit_jump = 0x1600;
            }

            if(__instance.m_state == Field.State.MAPJUMP && GS.FadeIsEnd() && !SaveData.IsCurrentSaveLoad() && ((___m_next_map_no & 65535) >> 10) == 19 && __instance.m_player.m_pic_no >= 8) { 
                __instance.UpdateNormal();
                if (GS.FadeIsEnd() && !SaveData.IsCurrentSaveLoad())
                {
                    int bgm = __instance.m_player.m_pic_no+1;
                    //bgm = bgm >= 19 && bgm <= 26 ? bgm + 8 : bgm;
                    ___m_next_map_no = ___m_next_map_no +  (bgm << 10);
                    __instance.MapJump(___m_next_map_no, ___m_next_map_x, ___m_next_map_y, ___m_next_map_dir, false);
                    __instance.UpdateNormal();
                    if (__instance.m_fader_on || __instance.m_fader_in_on)
                    {
                        GS.m_fade_delay = 2;
                        if (__instance.m_fade_white)
                        {
                            GS.FadeIn_White(20);
                        }
                        else
                        {
                            GS.FadeIn(20);
                        }
                        __instance.m_fader_in_on = false;
                        __instance.m_fader_on = true;
                    }
                    __instance.m_state = Field.State.MAPJUMP_END;
                    __instance.m_black_map = false;
                    __instance.m_fade_white = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(MessageWindow), "SetMessage")]
    public class FastDialog
    {
        public static void Prefix(MessageWindow __instance)
        {
            if(__instance.message_speed == 1)
                __instance.message_speed = Settings.instance.enTextSpeed;
            else if(__instance.message_speed == 2)
                __instance.message_speed = Settings.instance.jpTextSpeed;
        }
    }

    [HarmonyPatch(typeof(BattleLogic.BattleUnit), "grow")]
    public class FastGrow
    {
        public static void Prefix(ref int __state, BattleLogic.BattleUnit __instance)
        {
            __state = __instance.max_hp;
        }
        public static void Postfix(int id, ref int __state, BattleLogic.BattleUnit __instance)
        {
            if (Settings.instance.fastGrow <= 0 || Settings.instance.speedrun)
                return;
            switch (id)
            {
                case 0:
                    __instance.max_hp = Mathf.Min(__instance.max_hp + (__instance.max_hp - __state) * Settings.instance.fastGrow, 999);
                    break;
                case 1:
                    __instance.max_wp = Mathf.Min(__instance.max_wp + Settings.instance.fastGrow, 250);
                    break;
                case 2:
                    __instance.max_jp = Mathf.Min(__instance.max_jp + Settings.instance.fastGrow, 255);
                    break;
                case 3:
                    __instance._zoufuku_Lv = Mathf.Min(__instance._zoufuku_Lv + Settings.instance.fastGrowSkill, 50);
                    break;
                case 4:
                    __instance._skill_Lv.slash = Mathf.Min(__instance._skill_Lv.slash + Settings.instance.fastGrowSkill, 50);
                    break;
                case 5:
                    __instance._skill_Lv.beat = Mathf.Min(__instance._skill_Lv.beat + Settings.instance.fastGrowSkill, 50);
                    break;
                case 6:
                    __instance._skill_Lv.thrust = Mathf.Min(__instance._skill_Lv.thrust + Settings.instance.fastGrowSkill, 50);
                    break;
                case 7:
                    __instance._skill_Lv.shoot = Mathf.Min(__instance._skill_Lv.shoot + Settings.instance.fastGrowSkill, 50);
                    break;
                case 8:
                    __instance._skill_Lv.wrestle = Mathf.Min(__instance._skill_Lv.wrestle + Settings.instance.fastGrowSkill, 50);
                    break;
                case 9:
                    __instance._spell_Lv.chi.souryu = Mathf.Min(__instance._spell_Lv.chi.souryu + Settings.instance.fastGrowSkill, 50);
                    break;
                case 10:
                    __instance._spell_Lv.chi.syuchoh = Mathf.Min(__instance._spell_Lv.chi.syuchoh + Settings.instance.fastGrowSkill, 50);
                    break;
                case 11:
                    __instance._spell_Lv.chi.byakko = Mathf.Min(__instance._spell_Lv.chi.byakko + Settings.instance.fastGrowSkill, 50);
                    break;
                case 12:
                    __instance._spell_Lv.chi.genbu = Mathf.Min(__instance._spell_Lv.chi.genbu + Settings.instance.fastGrowSkill, 50);
                    break;
                case 13:
                    __instance._spell_Lv.ten.sun = Mathf.Min(__instance._spell_Lv.ten.sun + Settings.instance.fastGrowSkill, 50);
                    break;
                case 14:
                    __instance._spell_Lv.ten.moon = Mathf.Min(__instance._spell_Lv.ten.moon + Settings.instance.fastGrowSkill, 50);
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(PartyWork), "grow_enemys")]
    public class FastGrowEnemy
    {
        public static void Prefix(PartyWork __instance)
        {
            if (Settings.instance.speedrun)
                return;
            __instance._mon_counter[BattleWork.monster_kind_id] += Settings.instance.fastGrowEnemy;
        }
    }

    static class RandomExtensions
    {
        public static void Shuffle<T>(System.Random rng, T[,] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                int k1 = k / array.GetLength(1);
                int k2 = k % array.GetLength(1);
                int n1 = n / array.GetLength(1);
                int n2 = n % array.GetLength(1);
                T temp = array[n1, n2];
                array[n1, n2] = array[k1, k2];
                array[k1, k2] = temp;
            }
        }
        public static void Shuffle<T>(System.Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }

        public static void Assign<T>(T[] array, Dictionary<int, int> dict)
        {
            T[] array2 = new T[array.Length];
            Array.Copy(array, array2, array.Length);
            for(int i = 0; i < array.Length; i++)
            {
                array[i] = array2[dict[i]];
            }
        }

        public static void Assign(Dictionary<int, string> names, Dictionary<int, int> dict)
        {
            Dictionary<int, string> names2 = new Dictionary<int, string>(names);
            foreach(int i in dict.Keys)
            {
                names[i] = names2[dict[i]];
            }
        }

        public static void MoveAssign(DataStruct.Enemy.monster_base_data[] array, Dictionary<int, int> dict)
        {
            DataStruct.Enemy.monster_base_data[] array2 = new DataStruct.Enemy.monster_base_data[array.Length];
            Array.Copy(array, array2, array.Length);
            foreach(int i in dict.Keys)
            {
                array[i]._lank = array2[dict[i]]._lank;
                array[i]._slayer_id_0 = array2[dict[i]]._slayer_id_0;
                array[i]._slayer_id_1 = array2[dict[i]]._slayer_id_1;
                array[i]._heal_chisou = array2[dict[i]]._heal_chisou;
                array[i]._special_attribute_id_0 = array2[dict[i]]._special_attribute_id_0;
                array[i]._special_attribute_id_1 = array2[dict[i]]._special_attribute_id_1;
                array[i]._boss_special_attr_id = array2[dict[i]]._boss_special_attr_id;
                array[i]._act_1_or_2_ratio = array2[dict[i]]._act_1_or_2_ratio;
                array[i]._base_act_level = array2[dict[i]]._base_act_level;
                array[i]._equip_weapon_id_0 = array2[dict[i]]._equip_weapon_id_0;
                array[i]._can_mutoudori_0 = array2[dict[i]]._can_mutoudori_0;
                array[i]._equip_weapon_id_1 = array2[dict[i]]._equip_weapon_id_1;
                array[i]._can_mutoudori_1 = array2[dict[i]]._can_mutoudori_1;
                array[i]._equip_shield_id = array2[dict[i]]._equip_shield_id;
                array[i]._equip_armor_id_0 = array2[dict[i]]._equip_armor_id_0;
                array[i]._equip_armor_id_1 = array2[dict[i]]._equip_armor_id_1;
                array[i]._drop_item_table_id = array2[dict[i]]._drop_item_table_id;
                array[i]._act_table_id_0 = array2[dict[i]]._act_table_id_0;
                array[i]._act_table_id_1 = array2[dict[i]]._act_table_id_1;
            }
        }

        public static void EncounterShuffle(System.Random rng, DataStruct.EventBattleData.sp_battle_data[] array)
        {
            List<int> valids = new List<int>();
            List<int> valids2 = new List<int>();
            for(int i=0; i < array.Length; i++)
            {
                if (array[i]._main_monster_id_0 >= 0)
                {
                    valids.Add(i);
                    valids2.Add(i);
                }
            }
            DataStruct.EventBattleData.sp_battle_data[] array2 = new DataStruct.EventBattleData.sp_battle_data[array.Length];
            DataStruct.Enemy.monster_base_data[] array3 = new DataStruct.Enemy.monster_base_data[DataTable.monster_base_data_table.Length];
            Array.Copy(array, array2, array.Length);
            Array.Copy(DataTable.monster_base_data_table, array3, array3.Length);

            for (int i = 0; i < valids.Count; i++)
            {
                int kk = rng.Next(valids2.Count);
                int k = valids2[kk];
                int n = valids[i];
                valids2.RemoveAt(kk);
                //DataTable.monster_base_data_table[array2[k]._main_monster_id_0]._hp = array3[array[n]._main_monster_id_0]._hp;
                StatSwap(array2[k]._main_monster_id_0, array[n]._main_monster_id_0, array[n]._main_monster_id_0, array3);
                StatSwap(array2[k]._main_monster_id_1, array[n]._main_monster_id_1, array[n]._main_monster_id_0, array3);
                StatSwap(array2[k]._main_monster_id_2, array[n]._main_monster_id_2, array[n]._main_monster_id_0, array3);
                StatSwap(array2[k]._main_monster_id_3, array[n]._main_monster_id_3, array[n]._main_monster_id_0, array3);
                StatSwap(array2[k]._main_monster_id_4, array[n]._main_monster_id_4, array[n]._main_monster_id_0, array3);
                StatSwap(array2[k]._main_monster_id_5, array[n]._main_monster_id_5, array[n]._main_monster_id_0, array3);

                array[n]._main_monster_id_1 = array[n]._main_monster_id_1 == array[n]._main_monster_id_0 ? array2[k]._main_monster_id_1 : array[n]._main_monster_id_1;
                array[n]._main_monster_id_2 = array[n]._main_monster_id_2 == array[n]._main_monster_id_0 ? array2[k]._main_monster_id_2 : array[n]._main_monster_id_2;
                array[n]._main_monster_id_3 = array[n]._main_monster_id_3 == array[n]._main_monster_id_0 ? array2[k]._main_monster_id_3 : array[n]._main_monster_id_3;
                array[n]._main_monster_id_4 = array[n]._main_monster_id_4 == array[n]._main_monster_id_0 ? array2[k]._main_monster_id_4 : array[n]._main_monster_id_4;
                array[n]._main_monster_id_5 = array[n]._main_monster_id_5 == array[n]._main_monster_id_0 ? array2[k]._main_monster_id_5 : array[n]._main_monster_id_5;
                array[n]._main_monster_id_0 = array2[k]._main_monster_id_0;
                //array[n]._appear_num = Mathf.Min(array2[k]._appear_num, array[n]._appear_num);
                array[n]._hp_bairitu_0 = Mathf.Min(array2[k]._hp_bairitu_0, array[n]._hp_bairitu_0);
                array[n]._hp_bairitu_1 = Mathf.Min(array2[k]._hp_bairitu_1, array[n]._hp_bairitu_1);
            }
        }
        public static void StatSwap(int m1, int m2, int m0, DataStruct.Enemy.monster_base_data[] array3)
        {
            if(m1>=0 && m2 >= 0 && m0 == m2)
            {
                DataTable.monster_base_data_table[m1] = array3[m2];
            }
        }
    }

    //[HarmonyPatch(typeof(BattleLogic.InternalCalc), "byuunei_change_at_sky")]
    //public class SkyBuneFix
    //{
    //    public static void Prefix()
    //    {
    //        if (BattleWork.byuunei_btl_at_sky_flag && !GameCore.m_battle._enemy_mng[0].is_live() && Settings.instance.randomizer)
    //        {
    //            if (GameCore.m_battle._enemy_mng[0]._player_or_enemy_id - DataTable.sp_battle_data_table[20]._main_monster_id_0 >= 3)
    //            {
    //                GameCore.m_battle._enemy_mng[0]._player_or_enemy_id = 273;
    //            }
    //        }
    //    }
    //    public static void Postfix()
    //    {
    //        int hp = GameCore.m_battle._enemy_mng[0]._player_or_enemy_id - DataTable.sp_battle_data_table[20]._main_monster_id_0 >= 3 ? 9000 : 3000;
    //        if (BattleWork.byuunei_btl_at_sky_flag && GameCore.m_battle._enemy_mng[0].max_hp != hp && Settings.instance.randomizer)
    //        {
    //            GameCore.m_battle._enemy_mng[0].max_hp = hp;
    //            GameCore.m_battle._enemy_mng[0].hp = hp;
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(BattleLogic.InternalCalc), "byuunei_change_at_ground")]
    //public class GroundBuneFix
    //{
    //    public static void Prefix()
    //    {
    //        if (BattleWork.byuunei_btl_at_ground_flag && !GameCore.m_battle._enemy_mng[0].is_live() && Settings.instance.randomizer)
    //        {
    //            if (GameCore.m_battle._enemy_mng[0]._player_or_enemy_id - DataTable.sp_battle_data_table[19]._main_monster_id_0 >= 3)
    //            {
    //                GameCore.m_battle._enemy_mng[0]._player_or_enemy_id = 268;
    //            }
    //        }
    //    }
    //    public static void Postfix()
    //    {
    //        int hp = GameCore.m_battle._enemy_mng[0]._player_or_enemy_id - DataTable.sp_battle_data_table[19]._main_monster_id_0 >= 3 ? 9000 : 3000;
    //        if (BattleWork.byuunei_btl_at_ground_flag && GameCore.m_battle._enemy_mng[0].max_hp != hp && Settings.instance.randomizer)
    //        {
    //            GameCore.m_battle._enemy_mng[0].max_hp = hp;
    //            GameCore.m_battle._enemy_mng[0].hp = hp;
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(BattleLogic.InternalCalc), "last_btl_hp_check")]
    //public class FinalBossFix
    //{
    //    public static void Prefix(BattleLogic.InternalCalc __instance)
    //    {
    //        if (BattleWork.last_battle_flag && Settings.instance.randomizer)
    //        {
    //            BattleLogic.BattleUnit battleUnit = GameCore.m_battle._enemy_mng[0];
    //            int player_or_enemy_id = battleUnit._player_or_enemy_id;
    //            int firstid = DataTable.sp_battle_data_table[254]._main_monster_id_0;
    //            if (battleUnit.hp <= 0)
    //            {
    //                if (Utility_T_H.Math.is_in(player_or_enemy_id, firstid+5, firstid+8))
    //                {
    //                    GameCore.m_battle._enemy_mng[0]._player_or_enemy_id = 315;
    //                }
    //            }
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(BattleLogic.InternalCalc), "last_btl_last_boss_change_style")]
    //public class FinalBossFix2
    //{
    //    public static void Prefix(ref int __state)
    //    {
    //        if (BattleWork.last_battle_flag && Settings.instance.randomizer)
    //        {
    //            int firstid = DataTable.sp_battle_data_table[254]._main_monster_id_0;
    //            GameCore.m_battle._enemy_mng[0]._player_or_enemy_id += 310 - firstid;
    //            __state = GameCore.m_battle._enemy_mng[0]._player_or_enemy_id;
    //        }
    //    }
    //    public static void Postfix(ref int __state)
    //    {
    //        if (BattleWork.last_battle_flag && GameCore.m_battle._enemy_mng[0]._player_or_enemy_id == __state && Settings.instance.randomizer)
    //        {
    //            int firstid = DataTable.sp_battle_data_table[254]._main_monster_id_0;
    //            GameCore.m_battle._enemy_mng[0]._player_or_enemy_id -= 310 - firstid;
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(BattleLogic.InternalCalc), "last_boss_change")]
    //public class FinalBossFix3
    //{
    //    public static void Prefix(ref int id)
    //    {
    //        int firstid = DataTable.sp_battle_data_table[254]._main_monster_id_0;
    //        if (Settings.instance.randomizer)
    //            id -= 310 - firstid;
    //    }
    //}

    //[HarmonyPatch(typeof(BattleLogic.BattleUnit), "hp_damage")]
    //public class FinalBossFix4
    //{
    //    public static void Prefix(ref int value)
    //    {
    //        int firstid = DataTable.sp_battle_data_table[254]._main_monster_id_0;
    //        int id = GameCore.m_battle._enemy_mng[0]._player_or_enemy_id;
    //        if (Settings.instance.randomizer && BattleWork.last_battle_flag && Utility_T_H.Math.is_in(id, firstid, firstid + 4) || id == firstid + 9)
    //            value /= 10;
    //        else if (Utility_T_H.Math.is_in(id, 310, 314) || id == 319 && !BattleWork.last_battle_flag)
    //        {
    //            value *= 10;
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(Monster), "Load")]
    public class AssetSwap
    {
        public static void Prefix(ref int no)
        {
            int n = no - 1;
            File.WriteAllText("load.log", no.ToString());
            if (Randomizer.randomized && Randomizer.monsterDict.ContainsKey(n))
            {
                no = Randomizer.monsterDict[n] + 1;
                File.WriteAllText("swap.log", n.ToString() + ": " + DataTable.enemy_name_eng_table[n] + " to " + Randomizer.monsterDict[n].ToString() + ": " + DataTable.enemy_name_eng_table[Randomizer.monsterDict[n]]);
            }
        }
    }

    [HarmonyPatch(typeof(MapInfo), "GetMapInfo")]
    public class ChestRandomizer
    {
        public static Dictionary<string, List<MapInfo.TBox>> floors = new Dictionary<string, List<MapInfo.TBox>>();
        public static List<MapInfo.TBox> boxes = new List<MapInfo.TBox>();
        public static void Postfix(ref MapInfo __result)
        {
            if (!Randomizer.randomized)
                return;
            string key = GameCore.m_field.m_floor_info.data_name;
            if (floors.ContainsKey(key))
            {
                for(int i=0; i<__result.m_tbox.Count; i++)
                {
                    __result.m_tbox[i] = floors[key][i];
                }
            }
            return;
        }
    }

    [HarmonyPatch(typeof(DataTable), MethodType.StaticConstructor)]
    public class Randomizer
    {
        public static Dictionary<int, int> monsterDict = new Dictionary<int, int>();
        public static bool randomized = false;
        public static string seed = "0";
        public static System.Random rng;
        public static void Postfix()
        {
            List<int> monsters = new List<int>();
            rng = new System.Random(int.Parse(seed));
            for (int i = 0; i < DataTable.monster_base_data_table.Length; i++)
            {
                if(DataTable.monster_base_data_table[i]._lank>=0)
                    monsters.Add(i);
            }
            List<int> monsters2 = new List<int>(monsters);
            for (int i = 0; i < monsters2.Count; i++)
            {
                int r = rng.Next(monsters.Count);
                int k = monsters[r];
                monsterDict.Add(monsters2[i], k);
                monsters.RemoveAt(r);
            }
            RandomExtensions.MoveAssign(DataTable.monster_base_data_table, monsterDict);
            //RandomExtensions.Assign(DataTable.enemy_name_table, monsterDict);
            //RandomExtensions.Shuffle(rng, DataTable.item_armor_data_table);
            //RandomExtensions.Shuffle(rng, DataTable.item_weapon_data_table);
            //RandomExtensions.Shuffle(rng, DataTable.skill_player_table);
            randomized = true;
        }
    }

    //[HarmonyPatch(typeof(ScriptDrive), "ParseScript")]
    //public class ExtractScript
    //{
    //    public static void Postfix(string[] r3scriptRowIn)
    //    {
    //        File.WriteAllLines("r3script.txt", r3scriptRowIn);
    //    }
    //}

    public class EntryPoint
    {
        public static void Main(string[] args)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad += Domain_AssemblyLoad;
        }

        private static void Domain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly.GetName().Name == "Assembly-CSharp")
            {
                var harmony = new Harmony("com.rsaga3mod.qol");
                harmony.PatchAll();
            }
        }
    }
}
