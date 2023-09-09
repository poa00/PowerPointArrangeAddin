﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace ppt_arrange_addin {

    public partial class ArrangeRibbon {

        private Office.IRibbonUI _ribbon;

        public void Ribbon_Load(Office.IRibbonUI ribbonUi) {
            _ribbon = ribbonUi;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private struct Selection {
            public PowerPoint.ShapeRange ShapeRange { get; init; }
            public PowerPoint.Shape TextShape { get; init; }
            public PowerPoint.TextRange TextRange { get; init; }
            public PowerPoint.TextFrame TextFrame { get; init; }
            public PowerPoint.TextFrame2 TextFrame2 { get; init; }
        }

        private Selection GetSelection(bool onlyShapeRange) {
            // 1. application
            PowerPoint.Selection selection = null;
            try {
                var application = Globals.ThisAddIn.Application;
                if (application.Windows.Count > 0 && GetForegroundWindow().ToInt32() == application.HWND) {
                    selection = application.ActiveWindow.Selection;
                }
            } catch (Exception) { /* ignored */ }
            if (selection == null) {
                return new Selection();
            }

            // 2. shape range
            PowerPoint.ShapeRange shapeRange = null;
            if (selection.Type == PowerPoint.PpSelectionType.ppSelectionShapes) {
                shapeRange = selection.ShapeRange;
            } else if (selection.Type == PowerPoint.PpSelectionType.ppSelectionText) {
                try {
                    shapeRange = selection.ShapeRange;
                } catch (Exception) { /* ignored */ }
            }
            if (onlyShapeRange) {
                return new Selection { ShapeRange = shapeRange };
            }

            // 3. text range
            PowerPoint.TextRange textRange = null;
            PowerPoint.TextFrame textFrame = null;
            PowerPoint.Shape textShape = null;
            PowerPoint.TextFrame2 textFrame2 = null;
            if (selection.Type == PowerPoint.PpSelectionType.ppSelectionText) {
                textRange = selection.TextRange;
                if (textRange.Parent is PowerPoint.TextFrame frame) {
                    textFrame = frame;
                    if (textFrame.Parent is PowerPoint.Shape shape) {
                        textShape = shape;
                        textFrame2 = shape.TextFrame2;
                    }
                }
            } else if (shapeRange != null && shapeRange.HasTextFrame != Office.MsoTriState.msoFalse) {
                textFrame = shapeRange.TextFrame;
                textRange = textFrame.TextRange;
                textFrame2 = shapeRange.TextFrame2;
            }

            // 4. return selection
            return new Selection {
                ShapeRange = shapeRange,
                TextRange = textRange,
                TextShape = textShape,
                TextFrame = textFrame,
                TextFrame2 = textFrame2,
            };
        }

        private delegate bool AvailabilityRule(bool hasShapeRange, int shapesCount, bool hasTextFrame);
        private Dictionary<string, AvailabilityRule> _availabilityRules;

        private void InitializeAvailabilityRules() {
            _availabilityRules = new Dictionary<string, AvailabilityRule> {
                { btnAlignLeft, (_, cnt, _) => cnt >= 1 },
                { btnAlignCenter, (_, cnt, _) => cnt >= 1 },
                { btnAlignRight, (_, cnt, _) => cnt >= 1 },
                { btnAlignTop, (_, cnt, _) => cnt >= 1 },
                { btnAlignMiddle, (_, cnt, _) => cnt >= 1 },
                { btnAlignBottom, (_, cnt, _) => cnt >= 1 },
                { btnDistributeHorizontal, (_, cnt, _) => cnt >= 3 },
                { btnDistributeVertical, (_, cnt, _) => cnt >= 3 },
                { btnScaleSameWidth, (_, cnt, _) => cnt >= 2 },
                { btnScaleSameHeight, (_, cnt, _) => cnt >= 2 },
                { btnScaleSameSize, (_, cnt, _) => cnt >= 2 },
                { btnScalePosition, (_, cnt, _) => cnt >= 1 },
                { btnExtendSameLeft, (_, cnt, _) => cnt >= 2 },
                { btnExtendSameRight, (_, cnt, _) => cnt >= 2 },
                { btnExtendSameTop, (_, cnt, _) => cnt >= 2 },
                { btnExtendSameBottom, (_, cnt, _) => cnt >= 2 },
                { btnSnapLeft, (_, cnt, _) => cnt >= 2 },
                { btnSnapRight, (_, cnt, _) => cnt >= 2 },
                { btnSnapTop, (_, cnt, _) => cnt >= 2 },
                { btnSnapBottom, (_, cnt, _) => cnt >= 2 },
                { btnMoveForward, (_, cnt, _) => cnt >= 1 },
                { btnMoveFront, (_, cnt, _) => cnt >= 1 },
                { btnMoveBackward, (_, cnt, _) => cnt >= 1 },
                { btnMoveBack, (_, cnt, _) => cnt >= 1 },
                { btnRotateRight90, (_, cnt, _) => cnt >= 1 },
                { btnRotateLeft90, (_, cnt, _) => cnt >= 1 },
                { btnFlipVertical, (_, cnt, _) => cnt >= 1 },
                { btnFlipHorizontal, (_, cnt, _) => cnt >= 1 },
                { btnGroup, (_, cnt, _) => cnt >= 2 },
                { btnUngroup, (_, cnt, _) => cnt >= 1 },
                { edtShapePositionX, (_, cnt, _) => cnt >= 1 },
                { edtShapePositionY, (_, cnt, _) => cnt >= 1 },
                { btnShapePositionCopy, (_, cnt, _) => cnt == 1 },
                { btnShapePositionPaste, (_, cnt, _) => cnt >= 1 && _copiedPositionXPt >= 0 && _copiedPositionYPt >= 0 },
                { btnAutofitOff, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { btnAutofitText, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { btnAutoResize, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { btnWrapText, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { edtMarginLeft, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { edtMarginRight, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { edtMarginTop, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { edtMarginBottom, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { btnResetMarginHorizontal, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
                { btnResetMarginVertical, (_, cnt, hasTextFrame) => cnt >= 1 && hasTextFrame },
            };
        }

        public bool GetEnabled(Office.IRibbonControl ribbonControl) {
            var selection = GetSelection(onlyShapeRange: false);
            var hasShape = selection.ShapeRange != null;
            var shapesCount = selection.ShapeRange?.Count ?? 0;
            var hasTextFrame = selection.TextFrame != null;
            _availabilityRules.TryGetValue(ribbonControl.Id, out var checker);
            return checker?.Invoke(hasShape, shapesCount, hasTextFrame) ?? true;
        }

        public void AdjustRibbonButtonsAvailability(bool onlyForDrag = false) {
            if (!onlyForDrag) {
                _ribbon.Invalidate();
            } else {
                // currently callback that only for dragging to change the position is unavailable
                _ribbon.InvalidateControl(edtShapePositionX);
                _ribbon.InvalidateControl(edtShapePositionY);
            }
        }

        private PowerPoint.ShapeRange GetShapeRange(int mustMoreThanOrEqualTo = 1) {
            var selection = GetSelection(onlyShapeRange: true);
            var shapeRange = selection.ShapeRange;
            if (shapeRange == null || shapeRange.Count < mustMoreThanOrEqualTo) {
                return null;
            }
            return shapeRange;
        }

        private (PowerPoint.TextFrame, PowerPoint.TextFrame2) GetTextFrame() {
            var selection = GetSelection(onlyShapeRange: false);
            return (selection.TextFrame, selection.TextFrame2);
        }

        private void StartNewUndoEntry() {
            Globals.ThisAddIn.Application.StartNewUndoEntry();
        }

        public void BtnAlign_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            var cmd = ribbonControl.Id switch {
                btnAlignLeft => Office.MsoAlignCmd.msoAlignLefts,
                btnAlignCenter => Office.MsoAlignCmd.msoAlignCenters,
                btnAlignRight => Office.MsoAlignCmd.msoAlignRights,
                btnAlignTop => Office.MsoAlignCmd.msoAlignTops,
                btnAlignMiddle => Office.MsoAlignCmd.msoAlignMiddles,
                btnAlignBottom => Office.MsoAlignCmd.msoAlignBottoms,
                _ => throw new ArgumentException(nameof(BtnAlign_Click), nameof(ribbonControl.Id))
            };

            StartNewUndoEntry();
            var flag = shapeRange.Count == 1 ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse;
            shapeRange.Align(cmd, flag);
        }

        public void BtnDistribute_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange(mustMoreThanOrEqualTo: 3);
            if (shapeRange == null) {
                return;
            }

            var cmd = ribbonControl.Id switch {
                btnDistributeHorizontal => Office.MsoDistributeCmd.msoDistributeHorizontally,
                btnDistributeVertical => Office.MsoDistributeCmd.msoDistributeVertically,
                _ => throw new ArgumentException(nameof(BtnDistribute_Click), nameof(ribbonControl.Id))
            };

            StartNewUndoEntry();
            shapeRange.Distribute(cmd, Office.MsoTriState.msoFalse);
        }

        private Office.MsoScaleFrom _scaleFromFlag = Office.MsoScaleFrom.msoScaleFromMiddle; // used by BtnScale_Click

        public void BtnScalePosition_Click(Office.IRibbonControl ribbonControl) {
            _scaleFromFlag = _scaleFromFlag == Office.MsoScaleFrom.msoScaleFromMiddle
                ? Office.MsoScaleFrom.msoScaleFromTopLeft
                : Office.MsoScaleFrom.msoScaleFromMiddle;
            _ribbon.InvalidateControl(btnScalePosition);
        }

        public string GetBtnScalePositionLabel(Office.IRibbonControl ribbonControl) {
            return _scaleFromFlag == Office.MsoScaleFrom.msoScaleFromMiddle
                ? ArrangeRibbonResources.btnScalePosition_Middle
                : ArrangeRibbonResources.btnScalePosition_TopLeft;
        }

        public System.Drawing.Image GetBtnScalePositionImage(Office.IRibbonControl ribbonControl) {
            return _scaleFromFlag == Office.MsoScaleFrom.msoScaleFromMiddle
                ? Properties.Resources.ScaleFromMiddle
                : Properties.Resources.ScaleFromTopLeft;
        }

        public void BtnScale_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange(mustMoreThanOrEqualTo: 2);
            if (shapeRange == null) {
                return;
            }

            var shapes = shapeRange.OfType<PowerPoint.Shape>().ToArray();
            var (firstWidth, firstHeight) = (shapes[0].Width, shapes[0].Height); // select the first shape as final size
            var scaleFrom = _scaleFromFlag;

            StartNewUndoEntry();
            switch (ribbonControl.Id) {
            case btnScaleSameWidth:
                for (var i = 1; i < shapes.Length; i++) {
                    var shape = shapes[i];
                    var ratio = firstWidth / shape.Width;
                    shape.ScaleWidth(ratio, Office.MsoTriState.msoFalse, scaleFrom);
                }
                break;
            case btnScaleSameHeight:
                for (var i = 1; i < shapes.Length; i++) {
                    var shape = shapes[i];
                    var ratio = firstHeight / shape.Height;
                    shape.ScaleHeight(ratio, Office.MsoTriState.msoFalse, scaleFrom);
                }
                break;
            case btnScaleSameSize:
                for (var i = 1; i < shapes.Length; i++) {
                    var shape = shapes[i];
                    var ratio = firstWidth / shape.Width;
                    shape.ScaleWidth(ratio, Office.MsoTriState.msoFalse, scaleFrom);
                    ratio = firstHeight / shape.Height;
                    shape.ScaleHeight(ratio, Office.MsoTriState.msoFalse, scaleFrom);
                }
                break;
            }
        }

        public void BtnExtend_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange(mustMoreThanOrEqualTo: 2);
            if (shapeRange == null) {
                return;
            }

            var shapes = shapeRange.OfType<PowerPoint.Shape>().ToArray();
            float minLeft = 0x7fffffff, minTop = 0x7fffffff, maxLeftWidth = -1, maxTopHeight = -1;
            foreach (var shape in shapes) {
                minLeft = Math.Min(minLeft, shape.Left);
                minTop = Math.Min(minTop, shape.Top);
                maxLeftWidth = Math.Max(maxLeftWidth, shape.Left + shape.Width);
                maxTopHeight = Math.Max(maxTopHeight, shape.Top + shape.Height);
            }

            StartNewUndoEntry();
            switch (ribbonControl.Id) {
            case btnExtendSameLeft:
                foreach (var shape in shapes) {
                    var newWidth = shape.Width + shape.Left - minLeft;
                    var ratio = newWidth / shape.Width;
                    shape.ScaleWidth(ratio, Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromBottomRight);
                }
                break;
            case btnExtendSameRight:
                foreach (var shape in shapes) {
                    var newWidth = maxLeftWidth - shape.Left;
                    var ratio = newWidth / shape.Width;
                    shape.ScaleWidth(ratio, Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromTopLeft);
                }
                break;
            case btnExtendSameTop:
                foreach (var shape in shapes) {
                    var newTop = shape.Height + shape.Top - minTop;
                    var ratio = newTop / shape.Height;
                    shape.ScaleHeight(ratio, Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromBottomRight);
                }
                break;
            case btnExtendSameBottom:
                foreach (var shape in shapes) {
                    var newHeight = maxTopHeight - shape.Top;
                    var ratio = newHeight / shape.Height;
                    shape.ScaleHeight(ratio, Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromTopLeft);
                }
                break;
            }
        }

        public void BtnSnap_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange(mustMoreThanOrEqualTo: 2);
            if (shapeRange == null) {
                return;
            }

            var shapes = shapeRange.OfType<PowerPoint.Shape>().ToArray();
            var (previousLeft, previousTop) = (shapes[0].Left, shapes[0].Top);
            var (previousWidth, previousHeight) = (shapes[0].Width, shapes[0].Height);

            StartNewUndoEntry();
            switch (ribbonControl.Id) {
            case btnSnapLeft:
                for (var i = 1; i < shapes.Length; i++) {
                    shapes[i].Left = previousLeft + previousWidth;
                    previousLeft = shapes[i].Left;
                    previousWidth = shapes[i].Width;
                }
                break;
            case btnSnapRight:
                for (var i = 1; i < shapes.Length; i++) {
                    previousWidth = shapes[i].Width;
                    shapes[i].Left = previousLeft - previousWidth;
                    previousLeft = shapes[i].Left;
                }
                break;
            case btnSnapTop:
                for (var i = 1; i < shapes.Length; i++) {
                    shapes[i].Top = previousTop + previousHeight;
                    previousTop = shapes[i].Top;
                    previousHeight = shapes[i].Height;
                }
                break;
            case btnSnapBottom:
                for (var i = 1; i < shapes.Length; i++) {
                    previousHeight = shapes[i].Height;
                    shapes[i].Top = previousTop - previousHeight;
                    previousTop = shapes[i].Top;
                }
                break;
            default:
                return;
            }
        }

        public void BtnMove_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            var cmd = ribbonControl.Id switch {
                btnMoveForward => Office.MsoZOrderCmd.msoBringForward,
                btnMoveBackward => Office.MsoZOrderCmd.msoSendBackward,
                btnMoveFront => Office.MsoZOrderCmd.msoBringToFront,
                btnMoveBack => Office.MsoZOrderCmd.msoSendToBack,
                _ => throw new ArgumentException(nameof(BtnMove_Click), nameof(ribbonControl))
            };

            StartNewUndoEntry();
            shapeRange.ZOrder(cmd);
        }

        public void BtnRotate_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            StartNewUndoEntry();
            switch (ribbonControl.Id) {
            case btnRotateLeft90:
                shapeRange.IncrementRotation(-90);
                break;
            case btnRotateRight90:
                shapeRange.IncrementRotation(90);
                break;
            }
        }

        public void BtnFlip_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            StartNewUndoEntry();
            switch (ribbonControl.Id) {
            case btnFlipHorizontal:
                shapeRange.Flip(Office.MsoFlipCmd.msoFlipHorizontal);
                break;
            case btnFlipVertical:
                shapeRange.Flip(Office.MsoFlipCmd.msoFlipVertical);
                break;
            }
        }

        public void BtnGroup_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            switch (ribbonControl.Id) {
            case btnGroup:
                if (shapeRange.Count >= 2) {
                    StartNewUndoEntry();
                    var grouped = shapeRange.Group();
                    grouped.Select();
                    AdjustRibbonButtonsAvailability();
                }
                break;
            case btnUngroup:
                if (shapeRange.OfType<PowerPoint.Shape>().Any((s) => s.Type == Office.MsoShapeType.msoGroup)) {
                    StartNewUndoEntry();
                    var ungrouped = shapeRange.Ungroup();
                    ungrouped.Select();
                    AdjustRibbonButtonsAvailability();
                }
                break;
            }
        }

        private float CmToPt(float cm) => (float) (cm * 720 / 25.4);

        private float PtToCm(float pt) => (float) (pt * 25.4 / 720);

        public void EdtShapePosition_TextChanged(Office.IRibbonControl ribbonControl, string text) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            text = text.Replace("cm", "").Trim();
            if (text.Length == 0) {
                text = "0";
            }

            StartNewUndoEntry();
            if (float.TryParse(text, out var input)) {
                var pt = CmToPt(input);
                switch (ribbonControl.Id) {
                case edtShapePositionX:
                    shapeRange.Left = pt;
                    break;
                case edtShapePositionY:
                    shapeRange.Top = pt;
                    break;
                }
            }

            _ribbon.InvalidateControl(ribbonControl.Id);
        }

        public string GetEdtShapePositionText(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return "";
            }

            var pt = ribbonControl.Id switch {
                edtShapePositionX => shapeRange.Left,
                edtShapePositionY => shapeRange.Top,
                _ => -1
            };

            return pt < 0
                ? ""
                : $"{Math.Round(PtToCm(pt), 2)} cm";
        }

        private float _copiedPositionXPt = -1; // for shape and image
        private float _copiedPositionYPt = -1; // for shape and image

        public void BtnShapePositionCopy_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null || shapeRange.Count > 1) {
                return;
            }

            _copiedPositionXPt = shapeRange.Left;
            _copiedPositionYPt = shapeRange.Top;
            _ribbon.InvalidateControl(btnShapePositionPaste);
        }

        public void BtnShapePositionPaste_Click(Office.IRibbonControl ribbonControl) {
            var shapeRange = GetShapeRange();
            if (shapeRange == null) {
                return;
            }

            if (_copiedPositionXPt >= 0 && _copiedPositionYPt >= 0) {
                shapeRange.Left = _copiedPositionXPt;
                shapeRange.Top = _copiedPositionYPt;
            }
            _ribbon.InvalidateControl(edtShapePositionX);
            _ribbon.InvalidateControl(edtShapePositionY);
        }

        public void BtnAutofit_Click(Office.IRibbonControl ribbonControl, bool pressed) {
            var (_, textFrame) = GetTextFrame();
            if (textFrame == null) {
                return;
            }

            StartNewUndoEntry();
            textFrame.AutoSize = ribbonControl.Id switch {
                btnAutofitOff => Office.MsoAutoSize.msoAutoSizeNone,
                btnAutofitText => Office.MsoAutoSize.msoAutoSizeTextToFitShape,
                btnAutoResize => Office.MsoAutoSize.msoAutoSizeShapeToFitText,
                _ => textFrame.AutoSize
            };
            _ribbon.InvalidateControl(btnAutofitOff);
            _ribbon.InvalidateControl(btnAutofitText);
            _ribbon.InvalidateControl(btnAutoResize);
        }

        public bool GetBtnAutofitPressed(Office.IRibbonControl ribbonControl) {
            var (_, textFrame) = GetTextFrame();
            if (textFrame == null) {
                return false;
            }

            return ribbonControl.Id switch {
                btnAutofitOff => textFrame.AutoSize == Office.MsoAutoSize.msoAutoSizeNone,
                btnAutofitText => textFrame.AutoSize == Office.MsoAutoSize.msoAutoSizeTextToFitShape,
                btnAutoResize => textFrame.AutoSize == Office.MsoAutoSize.msoAutoSizeShapeToFitText,
                _ => false
            };
        }

        public void BtnWrapText_Click(Office.IRibbonControl ribbonControl, bool pressed) {
            var (textFrame, _) = GetTextFrame();
            if (textFrame == null) {
                return;
            }

            StartNewUndoEntry();
            textFrame.WordWrap = textFrame.WordWrap != Office.MsoTriState.msoTrue
                ? Office.MsoTriState.msoTrue
                : Office.MsoTriState.msoFalse;
            _ribbon.InvalidateControl(ribbonControl.Id);
        }

        public bool GetBtnWrapTextPressed(Office.IRibbonControl ribbonControl) {
            var (textFrame, _) = GetTextFrame();
            if (textFrame == null) {
                return false;
            }

            return textFrame.WordWrap == Office.MsoTriState.msoTrue;
        }

        private readonly float _defaultMarginHorizontalPt = 7.2F; // used by BtnResetMargin_Click
        private readonly float _defaultMarginVerticalPt = 3.6F;// used by BtnResetMargin_Click

        public void EdtMargin_TextChanged(Office.IRibbonControl ribbonControl, string text) {
            var (textFrame, _) = GetTextFrame();
            if (textFrame == null) {
                return;
            }

            text = text.Replace("cm", "").Trim();
            if (text.Length == 0) text = "0";

            StartNewUndoEntry();
            if (float.TryParse(text, out var input)) {
                var pt = CmToPt(input);
                switch (ribbonControl.Id) {
                case edtMarginLeft:
                    textFrame.MarginLeft = pt;
                    break;
                case edtMarginRight:
                    textFrame.MarginRight = pt;
                    break;
                case edtMarginTop:
                    textFrame.MarginTop = pt;
                    break;
                case edtMarginBottom:
                    textFrame.MarginBottom = pt;
                    break;
                }
            }

            _ribbon.InvalidateControl(ribbonControl.Id);
        }

        public string GetEdtMarginText(Office.IRibbonControl ribbonControl) {
            var (textFrame, _) = GetTextFrame();
            if (textFrame == null) {
                return "";
            }

            var pt = ribbonControl.Id switch {
                edtMarginLeft => textFrame.MarginLeft,
                edtMarginRight => textFrame.MarginRight,
                edtMarginTop => textFrame.MarginTop,
                edtMarginBottom => textFrame.MarginBottom,
                _ => -1
            };
            return pt < 0
                ? ""
                : $"{Math.Round(PtToCm(pt), 2)} cm";
        }

        public void BtnResetMargin_Click(Office.IRibbonControl ribbonControl) {
            var (textFrame, _) = GetTextFrame();
            if (textFrame == null) {
                return;
            }

            StartNewUndoEntry();
            switch (ribbonControl.Id) {
            case btnResetMarginHorizontal:
                textFrame.MarginLeft = _defaultMarginHorizontalPt;
                textFrame.MarginRight = _defaultMarginHorizontalPt;
                break;
            case btnResetMarginVertical:
                textFrame.MarginTop = _defaultMarginVerticalPt;
                textFrame.MarginBottom = _defaultMarginVerticalPt;
                break;
            }

            _ribbon.InvalidateControl(edtMarginLeft);
            _ribbon.InvalidateControl(edtMarginRight);
            _ribbon.InvalidateControl(edtMarginTop);
            _ribbon.InvalidateControl(edtMarginBottom);
        }

    }

}
