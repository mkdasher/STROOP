﻿using SM64_Diagnostic.Extensions;
using SM64_Diagnostic.Managers;
using SM64_Diagnostic.Structs;
using SM64_Diagnostic.Structs.Configurations;
using SM64_Diagnostic.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SM64_Diagnostic.Controls
{
    public class VarX
    {
        public readonly string Name;
        public readonly AddressHolder AddressHolder;

        private bool _editMode;
        private bool _highlighted;

        public bool EditMode
        {
            get
            {
                return _editMode;
            }
            set
            {
                _editMode = value;
                if (_textBox != null)
                {
                    _textBox.ReadOnly = !_editMode;
                    _textBox.BackColor = _editMode ? Color.White : _currentColor;
                    _textBox.ContextMenuStrip = _editMode ? _textboxOldContextMenuStrip : _contextMenuStrip;
                    if (_editMode)
                    {
                        _textBox.Focus();
                        _textBox.SelectAll();
                    }
                }
            }
        }

        private static readonly int FAILURE_DURATION_MS = 1000;
        private static readonly Color FAILURE_COLOR = Color.Red;
        private static readonly Color DEFAULT_COLOR = SystemColors.Control;

        private readonly Color _baseColor;
        private Color _currentColor;
        private bool _justFailed;
        private DateTime _lastFailureTime;



        public static VarX CreateVarX(
            string name,
            AddressHolder addressHolder,
            VarXSubclass varXSubclcass,
            Color? backgroundColor,
            bool invertBool = false)
        {
            switch (varXSubclcass)
            {
                case VarXSubclass.String:
                    return new VarX(name, addressHolder, backgroundColor);

                case VarXSubclass.Number:
                    return new VarXNumber(name, addressHolder, backgroundColor);

                case VarXSubclass.UnsignedAngle:
                    return new VarXAngle(name, addressHolder, backgroundColor, false);
                case VarXSubclass.SignedAngle:
                    return new VarXAngle(name, addressHolder, backgroundColor, true);

                case VarXSubclass.Object:
                    return new VarXObject(name, addressHolder, backgroundColor);

                case VarXSubclass.Boolean:
                    return new VarXBoolean(name, addressHolder, backgroundColor, invertBool);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public VarX(string name, AddressHolder addressHolder, Color? backgroundColor, bool useCheckbox = false)
        {
            Name = name;
            AddressHolder = addressHolder;
            _baseColor = backgroundColor ?? DEFAULT_COLOR;
            _currentColor = _baseColor;

            _editMode = false;
            _highlighted = false;
            _justFailed = false;
            _lastFailureTime = DateTime.Now;

            CreateControls(useCheckbox);
            AddContextMenuStripItems();
        }





        private BorderedTableLayoutPanel _tablePanel;
        protected Label _nameLabel;
        protected TextBox _textBox;
        protected CheckBox _checkBoxBool;

        protected ContextMenuStrip _textboxOldContextMenuStrip;
        protected ContextMenuStrip _contextMenuStrip;

        public Control Control
        {
            get
            {
                return _tablePanel;
            }
        }

        private void CreateControls(bool useCheckbox)
        {
            _nameLabel = new Label();
            _nameLabel.Size = new Size(210, 20); //TODO check this
            _nameLabel.Text = Name;
            _nameLabel.Margin = new Padding(3, 3, 3, 3);
            _nameLabel.Click += _nameLabel_Click;
            _nameLabel.ImageAlign = ContentAlignment.MiddleRight;
            _nameLabel.BackColor = Color.Transparent;

            _textBox = new TextBox();
            _textBox.ReadOnly = true;
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.TextAlign = HorizontalAlignment.Right;
            _textBox.Width = 200;
            _textBox.Margin = new Padding(6, 3, 6, 3);
            _textBox.KeyDown += OnTextValueKeyDown;
            _textBox.DoubleClick += _textBoxValue_DoubleClick;
            _textBox.Leave += (sender, e) => { EditMode = false; };

            _checkBoxBool = new CheckBox();
            _checkBoxBool.CheckAlign = ContentAlignment.MiddleRight;
            _checkBoxBool.CheckState = CheckState.Unchecked;
            _checkBoxBool.Click += (sender, e) => SetValueFromCheckbox(_checkBoxBool.CheckState);
            _checkBoxBool.BackColor = Color.Transparent;

            _tablePanel = new BorderedTableLayoutPanel();
            _tablePanel.Size = new Size(230, _nameLabel.Height + 2);
            _tablePanel.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            _tablePanel.RowCount = 1;
            _tablePanel.ColumnCount = 2;
            _tablePanel.RowStyles.Clear();
            _tablePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, _nameLabel.Height + 3));
            _tablePanel.ColumnStyles.Clear();
            _tablePanel.Margin = new Padding(0);
            _tablePanel.Padding = new Padding(0);
            _tablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            _tablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            _tablePanel.ShowBorder = false;
            _tablePanel.Controls.Add(_nameLabel, 0, 0);
            _tablePanel.Controls.Add(this._textBox, 1, 0);
            _tablePanel.Controls.Add(this._checkBoxBool, 1, 0);
            _tablePanel.BackColor = _currentColor;

            SetUseCheckbox(useCheckbox);

            _textboxOldContextMenuStrip = _textBox.ContextMenuStrip;
            _contextMenuStrip = new ContextMenuStrip();
            _nameLabel.ContextMenuStrip = _contextMenuStrip;
            _textBox.ContextMenuStrip = _contextMenuStrip;
            _tablePanel.ContextMenuStrip = _contextMenuStrip;
        }

        protected void AddContextMenuStripItems()
        {
            ToolStripMenuItem itemHighlight = new ToolStripMenuItem("Highlight");
            itemHighlight.Click += (sender, e) =>
            {
                _highlighted = !_highlighted;
                _tablePanel.ShowBorder = _highlighted;
                itemHighlight.Checked = _highlighted;
            };
            itemHighlight.Checked = _highlighted;

            ToolStripMenuItem itemEdit = new ToolStripMenuItem("Edit");
            itemEdit.Click += (sender, e) => { EditMode = true; };

            ToolStripMenuItem itemCopy = new ToolStripMenuItem("Copy");
            itemCopy.Click += (sender, e) => { Clipboard.SetText(GetValueForTextbox(false)); };

            ToolStripMenuItem itemPaste = new ToolStripMenuItem("Paste");
            itemPaste.Click += (sender, e) => { SetValueFromTextbox(Clipboard.GetText()); };

            _contextMenuStrip.Items.Add(itemHighlight);
            _contextMenuStrip.Items.Add(itemEdit);
            _contextMenuStrip.Items.Add(itemCopy);
            _contextMenuStrip.Items.Add(itemPaste);
        }

        private void _nameLabel_Click(object sender, EventArgs e)
        {
            VariableViewerForm varInfo;
            string typeDescr = AddressHolder.MemoryTypeName;

            varInfo = new VariableViewerForm(Name, typeDescr,
                String.Format("0x{0:X8}", AddressHolder.GetRamAddress()),
                String.Format("0x{0:X8}", AddressHolder.GetProcessAddress().ToUInt64()));

            varInfo.ShowDialog();
        }

        private void _textBoxValue_DoubleClick(object sender, EventArgs e)
        {
            EditMode = true;
        }

        private void InvokeFailure()
        {
            _justFailed = true;
            _lastFailureTime = DateTime.Now;
        }

        protected void SetUseCheckbox(bool useCheckbox)
        {
            if (useCheckbox)
            {
                _textBox.Visible = false;
                _checkBoxBool.Visible = true;
            }
            else
            {
                _textBox.Visible = true;
                _checkBoxBool.Visible = false;
            }
        }

        public void Update()
        {
            if (!_editMode)
            {
                _textBox.Text = GetValueForTextbox();
                _checkBoxBool.CheckState = GetValueForCheckbox();
            }

            UpdateColor();
        }

        private void UpdateColor()
        {
            if (_justFailed)
            {
                DateTime currentTime = DateTime.Now;
                double timeSinceLastFailure = currentTime.Subtract(_lastFailureTime).TotalMilliseconds;
                if (timeSinceLastFailure < FAILURE_DURATION_MS)
                {
                    _currentColor = ColorUtilities.InterpolateColor(
                        FAILURE_COLOR, _baseColor, timeSinceLastFailure / FAILURE_DURATION_MS);
                }
                else
                {
                    _currentColor = _baseColor;
                    _justFailed = false;
                }
            }

            _tablePanel.BackColor = _currentColor;
            if (!_editMode) _textBox.BackColor = _currentColor;
        }

        private void OnTextValueKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Escape)
            {
                EditMode = false;
                return;
            }

            if (e.KeyData == Keys.Enter)
            {
                bool success = SetValueFromTextbox(_textBox.Text);
                EditMode = false;
                if (!success)
                {
                    InvokeFailure();
                }
                return;
            }
        }






        public string GetValueForTextbox(bool handleRounding = true)
        {
            List<string> values = AddressHolder.GetValues();
            (bool meaningfulValue, string value) = CombineValues(values);
            if (!meaningfulValue) return value;

            value = HandleAngleConverting(value);
            if (handleRounding) value = HandleRounding(value);
            value = HandleAngleRoundingOut(value);
            value = HandleNegating(value);
            value = HandleHexDisplaying(value);
            value = HandleObjectDisplaying(value);

            return value;
        }

        public bool SetValueFromTextbox(string value)
        {
            value = HandleObjectUndisplaying(value);
            value = HandleHexUndisplaying(value);
            value = HandleUnnegating(value);
            value = HandleAngleUnconverting(value);

            return AddressHolder.SetValue(value);
        }


        private CheckState GetValueForCheckbox()
        {
            List<string> values = AddressHolder.GetValues();
            List<CheckState> checkStates = values.ConvertAll(value => ConvertValueToCheckState(value));
            CheckState checkState = CombineCheckStates(checkStates);
            return checkState;
        }

        private bool SetValueFromCheckbox(CheckState checkState)
        {
            string value = ConvertCheckStateToValue(checkState);
            return AddressHolder.SetValue(value);
        }




        protected (bool meaningfulValue, string stringValue) CombineValues(List<string> values)
        {
            if (values.Count == 0) return (false, "(none)");
            string firstValue = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] != firstValue) return (false, "multiple values");
            }
            return (true, firstValue);
        }

        protected CheckState CombineCheckStates(List<CheckState> checkStates)
        {
            if (checkStates.Count == 0) return CheckState.Unchecked;
            CheckState firstCheckState = checkStates[0];
            for (int i = 1; i < checkStates.Count; i++)
            {
                if (checkStates[i] != firstCheckState) return CheckState.Indeterminate;
            }
            return firstCheckState;
        }

        // Number methods

        public virtual string HandleRounding(string value)
        {
            return value;
        }

        public virtual string HandleNegating(string value)
        {
            return value;
        }

        public virtual string HandleUnnegating(string value)
        {
            return value;
        }

        public virtual string HandleHexDisplaying(string value)
        {
            return value;
        }

        public virtual string HandleHexUndisplaying(string value)
        {
            return value;
        }

        // Angle methods

        public virtual string HandleAngleConverting(string value)
        {
            return value;
        }

        public virtual string HandleAngleUnconverting(string value)
        {
            return value;
        }

        public virtual string HandleAngleRoundingOut(string value)
        {
            return value;
        }

        // Object methods

        public virtual string HandleObjectDisplaying(string value)
        {
            return value;
        }

        public virtual string HandleObjectUndisplaying(string value)
        {
            return value;
        }

        // Boolean methods

        public virtual CheckState ConvertValueToCheckState(string value)
        {
            return CheckState.Unchecked;
        }

        public virtual string ConvertCheckStateToValue(CheckState checkState)
        {
            return "";
        }


    }
}
