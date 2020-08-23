﻿using System;
using System.Windows;
using System.Windows.Controls;
using Codeer.TestAssistant.GeneratorToolKit;

namespace RM.Friendly.WPFStandardControls.Generator
{
    [CaptureCodeGenerator("RM.Friendly.WPFStandardControls.WPFDataGrid")]
    public class WPFDataGridGenerator : CaptureCodeGeneratorBase
    {
        DataGrid _control;

        protected override void Attach()
        {
            _control = (DataGrid)ControlObject;
            _control.CellEditEnding += CellEditEnding;
            _control.CurrentCellChanged += CurrentCellChanged;
        }

        protected override void Detach()
        {
            _control.CellEditEnding -= CellEditEnding;
            _control.CurrentCellChanged -= CurrentCellChanged;
        }

        public override bool ConvertChildClientPoint(ref System.Drawing.Point clientPointWinForms, out string childUIObject)
        {
            childUIObject = string.Empty;
            var clientPoint = new Point(clientPointWinForms.X, clientPointWinForms.Y);

            //指定座標の要素取得
            var hitElement = PointUtility.GetPosElement(clientPoint, _control);
            if (hitElement == null) return false;

            //DataGridCell取得
            DataGridCell cell = null;
            foreach (var x in TreeUtilityInTarget.VisualTree(hitElement, TreeRunDirection.Ancestors))
            {
                if (Equals(x, _control)) break;
                cell = x as DataGridCell;
                if (cell != null) break;
            }

            if (cell == null) return false;
            var row = DataGridRow.GetRowContainingElement(cell);
            if (row == null) return false;

            int rowindex = row.GetIndex();
            var colIndex = cell.Column.DisplayIndex;

            //座標変換
            var screenPos = _control.PointToScreen(clientPoint);
            var childPoint = cell.PointFromScreen(screenPos);
            clientPointWinForms.X = (int)childPoint.X;
            clientPointWinForms.Y = (int)childPoint.Y;

            childUIObject = $".GetCell({rowindex}, {colIndex})";
            return true;
        }

        void CurrentCellChanged(object sender, EventArgs e)
        {
            var current = _control.CurrentCell;
            if (current != null)
            {
                int row = -1;
                for (int i = 0; i < _control.Items.Count; i++)
                {
                    if (ReferenceEquals(_control.Items[i], current.Item))
                    {
                        row = i;
                        break;
                    }
                }
                if (row != -1)
                {
                    AddSentence(new TokenName(),
                        ".EmulateChangeCurrentCell(", row, ", ", current.Column.DisplayIndex,
                        new TokenAsync(CommaType.Before), ");");
                }
            }
        }

        void CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            int col = -1;
            for (int i = 0; i < _control.Columns.Count; i++)
            {
                if (ReferenceEquals(_control.Columns[i], e.Column))
                {
                    col = i;
                    break;
                }
            }

            if (e.EditingElement is TextBox)
            {
                AddSentence(new TokenName(),
                    ".EmulateChangeCellText(", _control.SelectedIndex, ", ", col, ", ",
                    GenerateUtility.ToLiteral(((TextBox)e.EditingElement).Text),
                    new TokenAsync(CommaType.Before), ");");
            }
            else if (e.EditingElement is ComboBox)
            {
                AddSentence(new TokenName(),
                    ".EmulateChangeCellComboSelect(", _control.SelectedIndex, ", ", col + ", ",
                    ((ComboBox)e.EditingElement).SelectedIndex,
                    new TokenAsync(CommaType.Before), ");");
            }
            else if (e.EditingElement is CheckBox)
            {
                bool? val = ((CheckBox)e.EditingElement).IsChecked;
                string valStr = val.HasValue && val.Value ? "true" : "false";
                AddSentence(new TokenName(),
                    ".EmulateCellCheck(", _control.SelectedIndex, ", ", col, ", ",
                    valStr,
                    new TokenAsync(CommaType.Before), ");");
            }
        }
    }
}
