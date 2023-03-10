using System;
using UnityEngine;
using HarmonyLib;
using HarmonyLib.Tools;
using DataStruct.Encount;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

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

        public static void Postfix(GameCore __instance, ref int __state)
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
        public static bool skillRando = true;
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
            if (GUI.Button(new Rect(108, 260, 96f, 32f), "look test"))
            {
                int looks1 = GameCore.m_scriptDrive.NpcNameToPicNumber(PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[int.Parse(Randomizer.seed)]._name);
                Character.m_pic_no_tbl[7] = (byte)looks1;
                Character.m_pic_d[7*4+3] = (byte)looks1;
            }
            GUI.Label(new Rect(8, 340, 200f, 64f), message);
            Randomizer.seed = GUI.TextField(new Rect(8, 260, 96f, 32f), Randomizer.seed);
            skillRando = GUI.Toggle(new Rect(8, 340, 96f, 32f), skillRando, "Skills");
            if(Randomizer.randomized)
                GUI.Label(new Rect(8, 300, 96f, 32f), "Randomized!");
            if (!Randomizer.randomized && GUI.Button(new Rect(8, 300, 96f, 32f), "Randomize"))
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
            if (GUI.Button(new Rect(108, 340, 96f, 32f), "Reload Room"))
            {
                GameCore.m_field.m_state = Field.State.MAPJUMP;
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
        public static void Shuffle<T>(System.Random rng, T[] array, int max=-1)
        {
            int n = max >= 0 ? max : array.Length;
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
            foreach (int i in dict.Keys)
            {
                array[i] = array2[dict[i]];
            }
        }

        public static void PriceAssign(DataStruct.Item.item_basic_data[] array, Dictionary<int, int> dict)
        {
            DataStruct.Item.item_basic_data[] array2 = new DataStruct.Item.item_basic_data[array.Length];
            Array.Copy(array, array2, array.Length);
            foreach (int i in dict.Keys)
            {
                array[dict[i]]._price = array2[i]._price;
            }
        }

        public static void SparkAssign(DataStruct.Skill.skill_hirameki_hasei_data[] array, Dictionary<int, int> dict)
        {
            DataStruct.Skill.skill_hirameki_hasei_data[] array2 = new DataStruct.Skill.skill_hirameki_hasei_data[array.Length];
            DataStruct.Skill.skill_player[] skp = new DataStruct.Skill.skill_player[DataTable.skill_player_table.Length];
            List<CommandSkillData> csdl = new List<CommandSkillData>();
            foreach(CommandSkillData c in BattleDataList.commandSkillDataList)
            {
                csdl.Add(new CommandSkillData(c.id.ToString(), c.costPoint, c.targetFlag.ToString(), c.useWeaponId.ToString(), c.nameJpn, c.nameEng, c.descJpn, c.descEng));
            }
            int n = 0;
            foreach(DataStruct.Skill.skill_player s in DataTable.skill_player_table)
            {
                skp[n] = new DataStruct.Skill.skill_player(s._kind_id, s._hit_correction, s._effect_value, s._hit_num, s._skill_grow, s._add_eff, s._slash_contain_flag, s._beat_contain_flag, s._thrust_contain_flag, s._shoot_contain_flag, s._heat_contain_flag, s._cold_contain_flag, s._thunder_contain_flag, s._magic_contain_flag, s._main_target, s._sub_target, s._all_flag, s._two_handed_flag, s._remote_flag, s._shieldchange_flag, s._masic_shield_can_flag, s._dodge_can_flag, s._counter_can_flag, s._shield_dodge_one_flag, s._shield_dodge_half_flag, s._kind_id_attack, s._routine, s._chisou_change_id, s._chisou_change_absolutely_flag, s._weapon_unique_skill_flag, s._got_effect_flag, s._blindnessgot_flag, s._counter_skill_flag, s._act_timing, s._usable_skill_Lv_id, s._usable_execution_level_id, s._use_wp, s._use_jp, s._item_lost);
                n++;
            }

            for(int i = 0; i < array.Length; i++)
            {
                for(int j = 0; j < 20; j++)
                {
                    if (array[i]._skill_hirameki_data[j]._id < 0)
                        break;
                    else if (dict.ContainsKey(array[i]._skill_hirameki_data[j]._id))
                    {
                        int s1 = array[i]._skill_hirameki_data[j]._id;
                        int s2 = dict[array[i]._skill_hirameki_data[j]._id];
                        DataTable.skill_player_table[s2]._usable_skill_Lv_id = skp[s1]._usable_skill_Lv_id;
                        //DataTable.skill_player_table[s2]._routine = skp[s1]._routine;
                        BattleDataList.commandSkillDataList[s2].useWeaponId = csdl[s1].useWeaponId;
                        array[i]._skill_hirameki_data[j]._id = s2;
                    }
                }
            }
        }

        public static void SkillAssign(DataStruct.Skill.skill_player[] array, Dictionary<int, int> dict)
        {
            DataStruct.Skill.skill_player[] array2 = new DataStruct.Skill.skill_player[array.Length];
            Array.Copy(array, array2, array.Length);
            foreach (int i in dict.Keys)
            {
                //array[i]._kind_id = array2[dict[i]]._kind_id;
                array[i]._hit_correction = array2[dict[i]]._hit_correction;
                //array[i]._effect_value = array2[dict[i]]._effect_value;
                array[i]._hit_num = array2[dict[i]]._hit_num;
                array[i]._skill_grow = array2[dict[i]]._skill_grow;
                array[i]._add_eff = array2[dict[i]]._add_eff;
                array[i]._slash_contain_flag = array2[dict[i]]._slash_contain_flag;
                array[i]._beat_contain_flag = array2[dict[i]]._beat_contain_flag;
                array[i]._thrust_contain_flag = array2[dict[i]]._thrust_contain_flag;
                array[i]._shoot_contain_flag = array2[dict[i]]._shoot_contain_flag;
                array[i]._heat_contain_flag = array2[dict[i]]._heat_contain_flag;
                array[i]._cold_contain_flag = array2[dict[i]]._cold_contain_flag;
                array[i]._thunder_contain_flag = array2[dict[i]]._thunder_contain_flag;
                array[i]._magic_contain_flag = array2[dict[i]]._magic_contain_flag;
                //array[i]._main_target = array2[dict[i]]._main_target;
                //array[i]._sub_target = array2[dict[i]]._sub_target;
                //array[i]._all_flag = array2[dict[i]]._all_flag;
                array[i]._two_handed_flag = array2[dict[i]]._two_handed_flag;
                array[i]._remote_flag = array2[dict[i]]._remote_flag;
                array[i]._shieldchange_flag = array2[dict[i]]._shieldchange_flag;
                array[i]._masic_shield_can_flag = array2[dict[i]]._masic_shield_can_flag;
                array[i]._dodge_can_flag = array2[dict[i]]._dodge_can_flag;
                array[i]._counter_can_flag = array2[dict[i]]._counter_can_flag;
                array[i]._shield_dodge_one_flag = array2[dict[i]]._shield_dodge_one_flag;
                array[i]._shield_dodge_half_flag = array2[dict[i]]._shield_dodge_half_flag;
                //array[i]._kind_id_attack = array2[dict[i]]._kind_id_attack;
                array[i]._routine = array2[dict[i]]._routine;
                array[i]._chisou_change_id = array2[dict[i]]._chisou_change_id;
                array[i]._chisou_change_absolutely_flag = array2[dict[i]]._chisou_change_absolutely_flag;
                //array[i]._weapon_unique_skill_flag = array2[dict[i]]._weapon_unique_skill_flag;
                array[i]._got_effect_flag = array2[dict[i]]._got_effect_flag;
                array[i]._blindnessgot_flag = array2[dict[i]]._blindnessgot_flag;
                //array[i]._counter_skill_flag = array2[dict[i]]._counter_skill_flag;
                array[i]._act_timing = array2[dict[i]]._act_timing;
                //array[i]._usable_skill_Lv_id = array2[dict[i]]._usable_skill_Lv_id;
                //array[i]._usable_execution_level_id = array2[dict[i]]._usable_execution_level_id;
                array[i]._use_wp = array2[dict[i]]._use_wp;
                array[i]._use_jp = array2[dict[i]]._use_jp;
                //array[i]._item_lost = array2[dict[i]]._item_lost;
            }
        }

        public static void ListAssign<T>(List<T> array, Dictionary<int, int> dict)
        {
            List<T> array2 = new List<T>(array);
            foreach (int i in dict.Keys)
            {
                array[i] = array2[dict[i]];
            }
        }

        public static void CommandAssign(List<CommandSkillData> array, Dictionary<int, int> dict)
        {
            List<CommandSkillData> array2 = new List<CommandSkillData>(array);
            foreach (int i in dict.Keys)
            {
                //array[i] = array2[dict[i]];
                //array[i].id = array2[dict[i]].id;
                array[dict[i]].useWeaponId = array2[i].useWeaponId;
                //array[i].targetFlag = array2[dict[i]].useWeaponId;
                //array[i].costDescEng = array2[dict[i]].costDescEng;
                //array[i].costDescJpn = array2[dict[i]].costDescJpn;
                //array[i].descEng = array2[dict[i]].descEng;
                //array[i].descJpn = array2[dict[i]].descJpn;
                //array[i].nameEng = array2[dict[i]].nameEng;
                //array[i].nameJpn = array2[dict[i]].nameJpn;
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

        public static void ParameterAssign(PlayerWorkDefault[] array, int a, PlayerWorkDefault b, int select=0)
        {
            if (select == 0)
                array[a]._strength = b._strength;
            else if (select == 1)
                array[a]._dexterity = b._dexterity;
            else if (select == 2)
                array[a]._agility = b._agility;
            else if (select == 3)
                array[a]._endure = b._endure;
            else if (select == 4)
                array[a]._force = b._force;
            else if (select == 5)
                array[a]._will = b._will;
            else if (select == 6)
                array[a]._fascination = b._fascination;
            else
            {
                //array[a]._number = b._number;
                //array[a]._name = b._name;
                array[a]._id = b._id;
                array[a]._id_name = b._id_name;
                array[a]._sex = b._sex;
                array[a]._slayer = b._slayer;
                array[a]._destiny_star = b._destiny_star;
                array[a]._destiny_star_name = b._destiny_star_name;
                array[a]._strong_point = b._strong_point;
                array[a]._strong_point_name = b._strong_point_name;
                array[a]._guardian = b._guardian;
                array[a]._guardian_name = b._guardian_name;
                array[a]._grade = b._grade;
                array[a]._hirameki = b._hirameki;
                array[a]._class = b._class;
                array[a]._hp = b._hp;
                array[a]._lp = b._lp;
                array[a]._slash = b._slash;
                array[a]._beat = b._beat;
                array[a]._thrust = b._thrust;
                array[a]._shoot = b._shoot;
                array[a]._wrestle = b._wrestle;
                array[a]._souryu = b._souryu;
                array[a]._syuchoh = b._syuchoh;
                array[a]._byakko = b._byakko;
                array[a]._genbu = b._genbu;
                array[a]._sun = b._sun;
                array[a]._moon = b._moon;
                array[a]._hp_up = b._hp_up;
                array[a]._lp_up = b._lp_up;
                array[a]._strength_up = b._strength_up;
                array[a]._dexterity_up = b._dexterity_up;
                array[a]._agility_up = b._agility_up;
                array[a]._endure_up = b._endure_up;
                array[a]._force_up = b._force_up;
                array[a]._will_up = b._will_up;
                array[a]._fascination_up = b._fascination_up;
                array[a]._slash_up = b._slash_up;
                array[a]._beat_up = b._beat_up;
                array[a]._thrust_up = b._thrust_up;
                array[a]._shoot_up = b._shoot_up;
                array[a]._wrestle_up = b._wrestle_up;
                array[a]._souryu_up = b._souryu_up;
                array[a]._syuchoh_up = b._syuchoh_up;
                array[a]._byakko_up = b._byakko_up;
                array[a]._genbu_up = b._genbu_up;
                array[a]._sun_up = b._sun_up;
                array[a]._moon_up = b._moon_up;
                array[a]._form_own = b._form_own;
                array[a]._form_own_name = b._form_own_name;
                array[a]._weapon0 = b._weapon0;
                array[a]._weapon0_name = b._weapon0_name;
                array[a]._weapon1 = b._weapon1;
                array[a]._weapon1_name = b._weapon1_name;
                array[a]._weapon2 = b._weapon2;
                array[a]._weapon2_name = b._weapon2_name;
                array[a]._weapon3 = b._weapon3;
                array[a]._weapon3_name = b._weapon3_name;
                array[a]._armor0 = b._armor0;
                array[a]._armor0_name = b._armor0_name;
                array[a]._armor1 = b._armor1;
                array[a]._armor1_name = b._armor1_name;
                array[a]._armor2 = b._armor2;
                array[a]._armor2_name = b._armor2_name;
                array[a]._armor3 = b._armor3;
                array[a]._armor3_name = b._armor3_name;
                array[a]._fix_armor = b._fix_armor;
                array[a]._fix_armor_name = b._fix_armor_name;
                array[a]._waza0 = b._waza0;
                array[a]._waza0_name = b._waza0_name;
                array[a]._waza1 = b._waza1;
                array[a]._waza1_name = b._waza1_name;
                array[a]._waza2 = b._waza2;
                array[a]._waza2_name = b._waza2_name;
                array[a]._jyutu0 = b._jyutu0;
                array[a]._jyutu0_name = b._jyutu0_name;
                array[a]._jyutu1 = b._jyutu1;
                array[a]._jyutu1_name = b._jyutu1_name;
                array[a]._jyutu2 = b._jyutu2;
                array[a]._jyutu2_name = b._jyutu2_name;
                array[a]._jyutu3 = b._jyutu3;
                array[a]._jyutu3_name = b._jyutu3_name;
                array[a]._jyutu4 = b._jyutu4;
                array[a]._jyutu4_name = b._jyutu4_name;
                array[a]._jyutu5 = b._jyutu5;
                array[a]._jyutu5_name = b._jyutu5_name;
                array[a]._cursor_def_page = b._cursor_def_page;
            }
        }

        public static void ParamaterShuffle(System.Random rng, PlayerWorkDefault[] array, Dictionary<int,int> dict, int max=-1)
        {
            PlayerWorkDefault[] array2 = new PlayerWorkDefault[array.Length];
            Array.Copy(array, array2, array.Length);
            int n = max >= 0 ? max : array.Length;

            for (int i = -1; i < 7; i++)
            {
                foreach(int j in dict.Keys) 
                {
                    if (i >= 0)
                    {
                        ParameterAssign(array, j, array2[rng.Next(n)], i);
                    }
                    else
                        ParameterAssign(array, j, array2[dict[j]], i);
                }
            }

            byte[] mpicopy = new byte[Character.m_pic_no_tbl.Length];
            Array.Copy(Character.m_pic_no_tbl, mpicopy, mpicopy.Length);

            foreach (int i in dict.Keys)
            {
                int looks1 = GameCore.m_scriptDrive.NpcNameToPicNumber(array[i]._name);
                int looks2 = GameCore.m_scriptDrive.NpcNameToPicNumber(array[dict[i]]._name);

                Character.m_pic_no_tbl[looks1] = mpicopy[looks2];
                Character.m_pic_d[looks1 * 4 + 3] = mpicopy[looks2];
            }
            Character.m_pic_no_tbl[7] = Character.m_pic_no_tbl[108];
            Character.m_pic_d[7 * 4 + 3] = Character.m_pic_no_tbl[108];

            for (int j = 33; j < 41; j++)
            {
                array[j] = array[18];
            }
            //for (int i = 0; i < n; i++)
            //    PlayerWork.Init(i, GameCore.m_playerWork);
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

    //[HarmonyPatch(MethodType.Constructor)]
    //public class ShopRandomizer
    //{
    //    static Type t;
    //    public static MethodBase TargetMethod()
    //    {
    //        t = AccessTools.TypeByName("ShopData");
    //        ConstructorInfo ci = t.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, new ParameterModifier[0]);
    //        return ci;
    //    }
    //    public static void Postfix()
    //    {
    //        GUIButtons.message = "Found it!\n";
    //        //FieldInfo instance = t.GetField("ms_instance", BindingFlags.Static | BindingFlags.NonPublic);
    //        Traverse instance = Traverse.Create(t).Field("ms_instance");
    //        var obj = instance.GetValue();
    //        GUIButtons.message += "hmm";
    //    }
    //}

    //[HarmonyPatch(typeof(MenuShopItemList), MethodType.Constructor, new Type[] { typeof(MENU_ID) })]
    //public class ShopRandomizer
    //{
    //    public static void Postfix(MENU_ID type, int[] ___m_syurui, int[] ___m_sub)
    //    {
    //        if (Randomizer.randomized && type == MENU_ID.SHOP_ITEM_BUY_LIST)
    //        {
    //            for (int i = 0; i < ___m_syurui.Length && i < ___m_sub.Length; i++)
    //            {
    //                ___m_syurui[i] = Randomizer.shopDict[___m_syurui[i]];
    //                ___m_sub[i] = DataTable.item_basic_data_table[___m_syurui[i]]._price;
    //            }
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(MenuManager), "GetNextMenu")]
    public class ShopRandomizer
    {
        public static int[] prices = new int[256];
        public static void Postfix(MENU_ID menu_id, MenuBase __result)
        {
            if (Randomizer.randomized && menu_id == MENU_ID.SHOP_ITEM_BUY_LIST)
            {
                var mid = __result.GetType().GetField("m_syurui", BindingFlags.NonPublic | BindingFlags.Instance);
                int[] m_syurui = mid.GetValue(__result) as int[];
                mid = __result.GetType().GetField("m_sub", BindingFlags.NonPublic | BindingFlags.Instance);
                int[] m_sub = mid.GetValue(__result) as int[];

                for (int i = 0; i < m_syurui.Length; i++)
                {
                    try
                    {
                        m_syurui[i] = Randomizer.shopDict[m_syurui[i]];
                        m_sub[i] = prices[m_syurui[i]];
                    }
                    catch (Exception e)
                    {
                        Dictionary<int, int> shopcopy = Randomizer.shopDict;
                        DataStruct.Item.item_basic_data[] itemcopy = DataTable.item_basic_data_table;
                        Console.WriteLine("accessed id: " + shopcopy[i]);
                        Console.WriteLine("itemtable size: " + itemcopy.Length);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(MenuShopArtList), "SetProductTable")]
    public class ShopRandomizerArts
    {
        public static void Prefix(ref int ___m_partyIdx, ref int __state)
        {
            if (Randomizer.randomized)
            {
                __state = ___m_partyIdx;
                ___m_partyIdx = 10000;
            }
        }
        public static void Postfix(ref MenuShopArtList __instance, ref int ___m_partyIdx, ref int __state)
        {
            var mid = __instance.GetType().GetField("m_type", BindingFlags.NonPublic | BindingFlags.Instance);
            MENU_ID? menu_id = mid.GetValue(__instance) as MENU_ID?;
            if (Randomizer.randomized && menu_id != null && (menu_id == MENU_ID.SHOP_ART_BUY_LIST || menu_id == MENU_ID.STRONG_ART_BUY_LIST))
            {
                var prop = __instance.GetType().GetField("m_productTable", BindingFlags.NonPublic | BindingFlags.Instance);
                int[] items = prop.GetValue(__instance) as int[];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = Randomizer.artsDict.ContainsKey(items[i]) ? Randomizer.artsDict[items[i]] : items[items[i]];
                }
                ___m_partyIdx = __state;
            }
        }
    }

    //[HarmonyPatch(typeof(MenuListText), "GetText", new Type[3] { typeof(int), typeof(int), typeof(int) })]
    //public class AttackNameSwap
    //{
    //    public static void Prefix(int sheet, ref int id)
    //    {
    //        if (Randomizer.randomized && sheet == 8 && Randomizer.skillDict.ContainsKey(id - 1))
    //        {
    //            id = Randomizer.skillDict[id - 1] + 1;
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(Character), "Chage")]
    public class RetainMainCharSprite
    {
        public static void Prefix(ref int __state)
        {
            __state = GameCore.m_playerWork[GameCore.m_partyWork._tsuikajun[0]]._looks;
        }

        public static void Postfix(ref int __state)
        {
            GameCore.m_playerWork[GameCore.m_partyWork._tsuikajun[0]]._looks = __state;
        }
    }

    [HarmonyPatch(typeof(ScriptDrive), nameof(ScriptDrive.DispatchCommand))]
    public class RetainMainCharSprite2
    {
        public static void Prefix(ref int __state)
        {
            __state = GameCore.m_playerWork[GameCore.m_partyWork._tsuikajun[0]]._looks;
        }

        public static void Postfix(ref int __state)
        {
            GameCore.m_playerWork[GameCore.m_partyWork._tsuikajun[0]]._looks = __state;
            //GameCore.m_field.m_player_pic_id = __state;
        }
    }

    [HarmonyPatch(typeof(ActionVM), "s_delete")]
    public class PreventMainCharDeletion
    {
        public static void Prefix(ref int ___npcIndex)
        {
            if (___npcIndex == 0)
                ___npcIndex = 1;
        }
    }

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

    //[HarmonyPatch(typeof(Character), "Init")]
    //public class CharacterSpriteSwap
    //{
    //    public static void Prefix()
    //    {
            
    //    }
    //}

    [HarmonyPatch(typeof(PlayerCleate), "_SetPlayerParam")]
    public class StartParamRandomizer
    {
        public static void Postfix()
        {
            PartyWork partyWork = GameCore.m_partyWork;
            int num = partyWork._tsuikajun[0];
            PlayerWork playerWork = GameCore.m_playerWork[num];

            playerWork._hp += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._hp - PlayerCleateBaseStatusData.status[num]._hp;
            playerWork._lp += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._lp - PlayerCleateBaseStatusData.status[num]._lp;
            playerWork._strength += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._strength - PlayerCleateBaseStatusData.status[num]._strength;
            playerWork._dexterity += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._dexterity - PlayerCleateBaseStatusData.status[num]._dexterity;
            playerWork._agility += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._agility - PlayerCleateBaseStatusData.status[num]._agility;
            playerWork._endure += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._endure - PlayerCleateBaseStatusData.status[num]._endure;
            playerWork._force += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._force - PlayerCleateBaseStatusData.status[num]._force;
            playerWork._will += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._will - PlayerCleateBaseStatusData.status[num]._will;
            playerWork._fascination += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._fascination - PlayerCleateBaseStatusData.status[num]._fascination;
            playerWork._slash += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._slash - PlayerCleateBaseStatusData.status[num]._slash;
            playerWork._beat += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._beat - PlayerCleateBaseStatusData.status[num]._beat;
            playerWork._thrust += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._thrust - PlayerCleateBaseStatusData.status[num]._thrust;
            playerWork._shoot += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._shoot - PlayerCleateBaseStatusData.status[num]._shoot;
            playerWork._wrestle += PlayerWorkDefaultDataTable.PlayerWorkDefaultTable[num]._wrestle - PlayerCleateBaseStatusData.status[num]._wrestle;
        }
    }

    [HarmonyPatch(typeof(DataTable), MethodType.StaticConstructor)]
    public class Randomizer
    {
        public static Dictionary<int, int> monsterDict = new Dictionary<int, int>();
        public static Dictionary<int, int> charDict = new Dictionary<int, int>();
        public static Dictionary<int, int> shopDict = new Dictionary<int, int>();
        public static Dictionary<int, int> artsDict = new Dictionary<int, int>();
        public static Dictionary<int, int> skillDict = new Dictionary<int, int>();
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
            List<int> items = new List<int>();
            for (int i = 0; i < DataTable.item_basic_data_table.Length; i++)
            {
                items.Add(i);
            }
            for (int i = 0; i < DataTable.item_basic_data_table.Length; i++)
            {
                int r = rng.Next(items.Count);
                int k = items[r];
                shopDict.Add(i, k);
                items.RemoveAt(r);
            }
            List<int> arts = new List<int>();
            for (int i = 170; i < 216; i++)
            {
                arts.Add(i);
            }
            for (int i = 170; i < 216; i++)
            {
                int r = rng.Next(arts.Count);
                int k = arts[r];
                artsDict.Add(i, k);
                arts.RemoveAt(r);
            }
            arts.Clear();
            CSVReader.AllDataMake();
            if (GUIButtons.skillRando)
            {
                for (int i = 10; i < 170; i++)
                {
                    if (BattleDataList.commandSkillDataList[i].descEng.Length > 2 && !DataTable.skill_player_table[i]._weapon_unique_skill_flag)
                        arts.Add(i);
                }
                List<int> arts2 = new List<int>(arts);
                for (int i = 0; i < arts2.Count; i++)
                {
                    if (BattleDataList.commandSkillDataList[arts2[i]].descEng.Length > 2)
                    {
                        int r = rng.Next(arts.Count);
                        int k = arts[r];
                        skillDict.Add(arts2[i], k);
                        arts.RemoveAt(r);
                    }
                }
            }
            List<int> chars = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                if (i != 27)
                    chars.Add(i);
            }
            for (int i = 0; i < 32; i++)
            {
                if (i != 27)
                {
                    int r = rng.Next(chars.Count);
                    int k = chars[r];
                    charDict[i] = k;
                    chars.RemoveAt(r);
                }
            }

            RandomExtensions.MoveAssign(DataTable.monster_base_data_table, monsterDict);
            RandomExtensions.PriceAssign(DataTable.item_basic_data_table, shopDict);
            for(int i = 0; i < 256; i++)
            {
                ShopRandomizer.prices[i] = DataTable.item_basic_data_table[i]._price;
            }

            RandomExtensions.ParamaterShuffle(rng, PlayerWorkDefaultDataTable.PlayerWorkDefaultTable, charDict, 32);

            if (GUIButtons.skillRando)
            {
                RandomExtensions.SparkAssign(DataTable.skill_hirameki_hasei_data_table, skillDict);
                //RandomExtensions.Assign(DataTable.skill_hirameki_type_data_table, skillDict);
                //RandomExtensions.CommandAssign(BattleDataList.commandSkillDataList, skillDict);
                //RandomExtensions.SkillAssign(DataTable.skill_player_table, skillDict);
                //RandomExtensions.Assign(BattleSkillDataTable.BattleSkillTable, skillDict);
            }

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
