using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using ImGuiNET;
using OpenSage.Gui.Apt.ActionScript;
using OpenSage.Gui.Apt.ActionScript.Opcodes;
using OpenSage.Tools.AptEditor.Apt.Editor;
using OpenSage.Tools.AptEditor.Util;
using ValueType = OpenSage.Gui.Apt.ActionScript.ValueType;

namespace OpenSage.Tools.AptEditor.UI.Widgets
{
    internal class InstructionEditor : IWidget
    {
        LogicalInstructions _instructions;
        InputComboBox _editBox = new InputComboBox(InstructionUtility.InstructionNames);
        int _editingIndex = -1;

        public InstructionEditor(LogicalInstructions instructions)
        {
            _instructions = instructions;
        }

        public void Draw(AptSceneManager manager)
        {
            if(ImGui.Begin("Instruction Editor"))
            {
                for (var i = 0; i < _instructions.Items.Count; ++i)
                {
                    if(i == _editingIndex)
                    {
                        _editBox.Draw();
                        ImGui.SameLine();
                        if(ImGui.Button("Done"))
                        {
                            _editingIndex = -1;
                        }
                    }
                    else
                    {
                        ImGui.Button(_instructions.Items[i].InstructionName());
                    }
                }
            }
            ImGui.End();
        }
    }

    internal class InputComboBox
    {
        public string ID { get; set; }
        public string PopUpID => $"##{ID}.Suggestions";
        public IEnumerable<string> Suggestions { get; set; }
        private byte[] _buffer;
        private string _current;
        private bool _listing;

        public InputComboBox(IEnumerable<string> suggestions)
        {
            _buffer = new byte[32];
            Suggestions = suggestions;
        }

        public void Draw()
        {
            ImGuiUtility.InputText($"##{ID}", _buffer, out _current);
            if(ImGui.IsItemActive() && _current.Any())
            {
                var inputPosition = ImGui.GetItemRectMin();
                var inputSize = ImGui.GetItemRectSize();
                var popUpPosition = new Vector2(inputPosition.X, inputPosition.Y + inputSize.Y);
                var popUpSize = new Vector2(inputSize.X, inputSize.Y * 5);
                DrawSuggestions(popUpPosition, popUpSize);
            }
            else
            {
                _listing = false;
            }
        }

        /// Inspired by Harold Brenes' auto complete widget https://github.com/ocornut/imgui/issues/718
        private void DrawSuggestions(Vector2 position, Vector2 size)
        {
            if(Suggestions == null)
            {
                return;
            }

            const StringComparison noCase = StringComparison.OrdinalIgnoreCase;

            var topSuggestions = Suggestions.Where(input => input.StartsWith(_current, noCase));
            var otherSuggestions = Suggestions.Where(input => !input.StartsWith(_current, noCase) && input.Contains(_current, noCase));
            var allSuggestions = topSuggestions.Concat(otherSuggestions);
            if(!allSuggestions.Any())
            {
                return;
            }

            if(!_listing)
            {
                _listing = true;
            }

            ImGui.SetNextWindowPos(position);
            ImGui.SetNextWindowSize(size);

            const ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.HorizontalScrollbar |
                ImGuiWindowFlags.NoSavedSettings;

            if(ImGui.Begin(PopUpID, flags))
            {
                ImGui.PushAllowKeyboardFocus(false);

                foreach(var suggestion in allSuggestions)
                {
                    if(ImGui.Selectable(suggestion))
                    {
                        Encoding.UTF8.GetBytes(suggestion).CopyTo(_buffer, 0);
                        // ImGui::SetScrollHere();
                    }
                }
                ImGui.PopAllowKeyboardFocus();
            }
            ImGui.End();
        }
    }

}