﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SM64_Diagnostic.Utilities;
using System.Windows.Forms;
using SM64_Diagnostic.Structs;
using SM64_Diagnostic.Structs.Configurations;

namespace SM64_Diagnostic.Managers
{
    public class DebugManager
    {
        CheckBox _spawnDebugCheckbox, _classicCheckbox, _resourceCheckbox, _stageSelectCheckbox, _freeMovementCheckbox;
        RadioButton[] _dbgSettingRadioButton;
        RadioButton _dbgSettingRadioButtonOff;

        public DebugManager(Control tabControl)
        {
            var panel = tabControl.Controls["NoTearFlowLayoutPanelDebugDisplayType"];

            _spawnDebugCheckbox = tabControl.Controls["checkBoxDbgSpawnDbg"] as CheckBox;
            _spawnDebugCheckbox.Click += (sender, e) =>
            {
                Config.Stream.SetValue(_spawnDebugCheckbox.Checked ? (byte)0x03 : (byte)0x00, Config.Debug.SettingAddress);
                Config.Stream.SetValue(_spawnDebugCheckbox.Checked ? (byte)0x01 : (byte)0x00, Config.Debug.SpawnModeAddress);
            };

            _classicCheckbox = tabControl.Controls["checkBoxDbgClassicDbg"] as CheckBox;
            _classicCheckbox.Click += (sender, e) =>
            {
                Config.Stream.SetValue(_classicCheckbox.Checked ? (byte)0x01 : (byte)0x00, Config.Debug.ClassicModeAddress);
            };

            _resourceCheckbox = tabControl.Controls["checkBoxDbgResource"] as CheckBox;
            _resourceCheckbox.Click += (sender, e) =>
            {
                Config.Stream.SetValue(_resourceCheckbox.Checked ? (byte)0x01 : (byte)0x00, Config.Debug.ResourceModeAddress);
            };

            _stageSelectCheckbox = tabControl.Controls["checkBoxDbgStageSelect"] as CheckBox;
            _stageSelectCheckbox.Click += (sender, e) =>
            {
                Config.Stream.SetValue(_stageSelectCheckbox.Checked ? (byte)0x01 : (byte)0x00, Config.Debug.StageSelectAddress);
            };

            _freeMovementCheckbox = tabControl.Controls["checkBoxDbgFreeMovement"] as CheckBox;
            _freeMovementCheckbox.Click += (sender, e) => 
            {
                Config.Stream.SetValue(
                    _freeMovementCheckbox.Checked ? Config.Debug.FreeMovementOnValue : Config.Debug.FreeMovementOffValue,
                    Config.Debug.FreeMovementAddress);
            };

            _dbgSettingRadioButtonOff = panel.Controls["radioButtonDbgOff"] as RadioButton;
            _dbgSettingRadioButtonOff.Click += (sender, e) =>
            {
                Config.Stream.SetValue((byte)0, Config.Debug.AdvancedModeAddress);
                Config.Stream.SetValue((byte)0, Config.Debug.SettingAddress);
            };

            _dbgSettingRadioButton = new RadioButton[6];
            _dbgSettingRadioButton[0] = panel.Controls["radioButtonDbgObjCnt"] as RadioButton;
            _dbgSettingRadioButton[1] = panel.Controls["radioButtonDbgChkInfo"] as RadioButton;
            _dbgSettingRadioButton[2] = panel.Controls["radioButtonDbgMapInfo"] as RadioButton;
            _dbgSettingRadioButton[3] = panel.Controls["radioButtonDbgStgInfo"] as RadioButton;
            _dbgSettingRadioButton[4] = panel.Controls["radioButtonDbgFxInfo"] as RadioButton;
            _dbgSettingRadioButton[5] = panel.Controls["radioButtonDbgEnemyInfo"] as RadioButton;
            for (int i = 0; i < _dbgSettingRadioButton.Length; i++)
            {
                byte localIndex = (byte)i;
                _dbgSettingRadioButton[i].Click += (sender, e) =>
                {
                    Config.Stream.SetValue((byte)1, Config.Debug.AdvancedModeAddress);
                    Config.Stream.SetValue(localIndex, Config.Debug.SettingAddress);
                };
            }
        }

        public void Update(bool updateView = false)
        {
            if (!updateView)
                return;

            _spawnDebugCheckbox.Checked = Config.Stream.GetByte(Config.Debug.SettingAddress) == 0x03
                 && Config.Stream.GetByte(Config.Debug.SpawnModeAddress) == 0x01;
            _classicCheckbox.Checked = Config.Stream.GetByte(Config.Debug.ClassicModeAddress) == 0x01;
            _resourceCheckbox.Checked = Config.Stream.GetByte(Config.Debug.ResourceModeAddress) == 0x01;
            _stageSelectCheckbox.Checked = Config.Stream.GetByte(Config.Debug.StageSelectAddress) == 0x01;
            _freeMovementCheckbox.Checked = Config.Stream.GetUInt16(Config.Debug.FreeMovementAddress) == Config.Debug.FreeMovementOnValue;

            var setting = Config.Stream.GetByte(Config.Debug.SettingAddress);
            var on = Config.Stream.GetByte(Config.Debug.AdvancedModeAddress);
            if (on % 2 != 0)
            {
                if (setting > 0 && setting < _dbgSettingRadioButton.Length)
                    _dbgSettingRadioButton[setting].Checked = true;
                else
                    _dbgSettingRadioButton[0].Checked = true;
            }
            else
            {
                _dbgSettingRadioButtonOff.Checked = true;
            }
        }
    }
}
