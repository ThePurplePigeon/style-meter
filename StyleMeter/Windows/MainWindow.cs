using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StyleMeter.Tracking;

namespace StyleMeter.Windows;

public class MainWindow : Window
{
    private readonly Plugin plugin;
    private readonly StyleMeterOverlayAnimationState animationState = new();
    private float lastTopCenterAnchorOffsetX;

    public MainWindow(Plugin plugin)
        : base(
            "Style Meter###StyleMeterOverlay",
            StyleMeterOverlayWindowFlags.Base)
    {
        this.plugin = plugin;
    }

    public override void PreDraw()
    {
        var visibility = StyleMeterOverlayVisibility.Resolve(
            this.plugin.Configuration.ShowOverlay,
            this.plugin.Configuration.AutoHideOutsideCombat,
            this.plugin.Tracker.IsInCombat,
            this.plugin.PreviewCombatOverlay);
        Flags = StyleMeterOverlayWindowFlags.Create(
            StyleMeterOverlayInputMode.Resolve(
                this.plugin.Configuration.LockOverlay,
                this.plugin.Configuration.ClickThroughOverlay || !visibility.ShouldDraw,
                visibility.UsePreviewSnapshot));
    }

    public override void Draw()
    {
        var visibility = StyleMeterOverlayVisibility.Resolve(
            this.plugin.Configuration.ShowOverlay,
            this.plugin.Configuration.AutoHideOutsideCombat,
            this.plugin.Tracker.IsInCombat,
            this.plugin.PreviewCombatOverlay);
        if (!visibility.ShouldDraw)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var snapshot = visibility.UsePreviewSnapshot
            ? StyleMeterPreviewSnapshot.CreateCombat(nowUtc)
            : this.plugin.Tracker.CurrentSnapshot;
        var scale = StyleMeterOverlayMath.NormalizeOverlayScale(this.plugin.Configuration.OverlayScale);
        var options = StyleMeterOverlayOptions.FromConfiguration(this.plugin.Configuration);
        var animationTimeSeconds = ImGui.GetTime();
        var renderState = this.animationState.Update(snapshot, animationTimeSeconds);
        this.ApplyTopCenterAnchor(snapshot, nowUtc, scale);

        StyleMeterOverlayRenderer.Draw(snapshot, nowUtc, animationTimeSeconds, scale, renderState, options);
    }

    private void ApplyTopCenterAnchor(StyleMeterSnapshot snapshot, DateTime nowUtc, float scale)
    {
        var anchorOffsetX = StyleMeterOverlayAnchor.GetTopCenterAnchorOffsetX(snapshot, nowUtc, scale);
        var offsetDeltaX = anchorOffsetX - this.lastTopCenterAnchorOffsetX;
        this.lastTopCenterAnchorOffsetX = anchorOffsetX;

        if (MathF.Abs(offsetDeltaX) <= 0.01f)
        {
            return;
        }

        ImGui.SetWindowPos(ImGui.GetWindowPos() + new Vector2(offsetDeltaX, 0f));
    }
}

internal static class StyleMeterOverlayRenderer
{
    public static void Draw(
        StyleMeterSnapshot snapshot,
        DateTime nowUtc,
        double animationTimeSeconds,
        float scale,
        StyleMeterOverlayRenderState renderState = default,
        StyleMeterOverlayOptions? options = null)
    {
        var safeScale = StyleMeterOverlayMath.NormalizeOverlayScale(scale);
        var safeOptions = StyleMeterOverlayOptions.Normalize(options ?? StyleMeterOverlayOptions.Default);
        if (StyleMeterOverlayMath.ShouldDrawIdle(snapshot))
        {
            DrawIdle(snapshot, safeScale, animationTimeSeconds, safeOptions);
            return;
        }

        DrawActive(snapshot, nowUtc, animationTimeSeconds, safeScale, renderState, safeOptions);
    }

    private static void DrawActive(
        StyleMeterSnapshot snapshot,
        DateTime nowUtc,
        double animationTimeSeconds,
        float scale,
        StyleMeterOverlayRenderState renderState,
        StyleMeterOverlayOptions options)
    {
        if (snapshot.IsFading)
        {
            DrawEndingTransition(snapshot, nowUtc, animationTimeSeconds, scale, options);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var layout = StyleMeterOverlayLayout.CreateActive(scale);
        var canvasOrigin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(layout.CanvasSize);

        var panel = layout.Translate(layout.Panel, canvasOrigin);
        var rankMedallion = layout.Translate(layout.RankMedallion, canvasOrigin);
        var statusChip = layout.Translate(layout.StatusChip, canvasOrigin);
        var bestBlock = layout.Translate(layout.BestBlock, canvasOrigin);
        var timerRail = layout.Translate(layout.TimerRail, canvasOrigin);
        var alpha = options.Opacity;
        var pulse = StyleMeterOverlayMath.GetPulseIntensity(animationTimeSeconds, options.AnimationIntensity);
        var milestoneFlash = StyleMeterOverlayMath.NormalizeUnit(renderState.MilestoneFlashIntensity * options.AnimationIntensity);
        var rankAccent = StyleMeterOverlayPalette.GetRankColor(snapshot.Rank, alpha);
        var accent = StyleMeterOverlayPalette.Blend(rankAccent, new Vector4(1f, 1f, 1f, alpha), milestoneFlash * 0.35f);
        var accentSoft = StyleMeterOverlayPalette.GetRankColor(snapshot.Rank, alpha * (0.26f + (pulse * 0.16f)));
        var timerProgress = StyleMeterOverlayMath.GetTimerProgress(snapshot, nowUtc);
        var dangerIntensity = StyleMeterOverlayMath.GetTimerDangerIntensity(timerProgress, pulse);
        var timerColor = StyleMeterOverlayPalette.GetTimerColor(rankAccent, dangerIntensity, alpha);

        DrawPanel(drawList, panel, accent, accentSoft, alpha, scale);
        DrawMilestoneFlash(drawList, panel, rankMedallion, rankAccent, alpha, milestoneFlash, scale);
        DrawRankMedallion(drawList, rankMedallion, snapshot.Rank, accent, alpha, pulse, scale);
        DrawChainText(drawList, layout, canvasOrigin, snapshot, accent, alpha, scale, options.ShowChainDetails);
        if (options.ShowBestBlock)
        {
            DrawBestBlock(drawList, bestBlock, snapshot.BestComboCount, accent, alpha, scale);
        }

        DrawStatusChip(drawList, statusChip, GetStatusText(snapshot, dangerIntensity), timerColor, alpha, scale);
        DrawTimerRail(drawList, timerRail, timerProgress, timerColor, dangerIntensity, alpha, scale);
    }

    private static void DrawEndingTransition(
        StyleMeterSnapshot snapshot,
        DateTime nowUtc,
        double animationTimeSeconds,
        float scale,
        StyleMeterOverlayOptions options)
    {
        var drawList = ImGui.GetWindowDrawList();
        var transitionProgress = StyleMeterOverlayMath.GetEndTransitionProgress(snapshot, nowUtc);
        var layout = StyleMeterOverlayLayout.CreateTransition(scale, transitionProgress);
        var canvasOrigin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(layout.CanvasSize);

        var panel = layout.Translate(layout.Panel, canvasOrigin);
        var rankMedallion = layout.Translate(layout.RankMedallion, canvasOrigin);
        var statusChip = layout.Translate(layout.StatusChip, canvasOrigin);
        var bestBlock = layout.Translate(layout.BestBlock, canvasOrigin);
        var timerRail = layout.Translate(layout.TimerRail, canvasOrigin);
        var pulse = StyleMeterOverlayMath.GetPulseIntensity(animationTimeSeconds, options.AnimationIntensity);
        var activeContentAlpha = StyleMeterOverlayMath.GetEndingActiveContentAlpha(transitionProgress) * options.Opacity;
        var idleContentAlpha = StyleMeterOverlayMath.GetEndingIdleContentAlpha(transitionProgress) * options.Opacity;
        var panelAlpha = 0.9f * options.Opacity;
        var rankAccent = StyleMeterOverlayPalette.GetRankColor(snapshot.Rank, panelAlpha);
        var idleAccent = StyleMeterOverlayPalette.GetRankColor("D", panelAlpha);
        var accent = StyleMeterOverlayPalette.Blend(rankAccent, idleAccent, transitionProgress);
        var softAlpha = options.Opacity * (0.22f + (pulse * 0.08f));
        var accentSoft = StyleMeterOverlayPalette.Blend(
            StyleMeterOverlayPalette.GetRankColor(snapshot.Rank, softAlpha),
            StyleMeterOverlayPalette.GetRankColor("D", softAlpha),
            transitionProgress);

        DrawPanel(drawList, panel, accent, accentSoft, panelAlpha, scale);
        DrawRankMedallionBase(drawList, rankMedallion, accent, 0.82f * options.Opacity, pulse, scale);
        DrawRankText(drawList, rankMedallion, snapshot.Rank, activeContentAlpha, scale);
        DrawRankText(drawList, rankMedallion, "D", idleContentAlpha, scale);

        if (activeContentAlpha > 0.01f)
        {
            var timerColor = StyleMeterOverlayPalette.GetTimerColor(rankAccent, 0f, activeContentAlpha);
            DrawChainText(drawList, layout, canvasOrigin, snapshot, accent, activeContentAlpha, scale, options.ShowChainDetails);
            DrawStatusChip(drawList, statusChip, "ENDED", timerColor, activeContentAlpha, scale);
            DrawTimerRail(drawList, timerRail, 0f, timerColor, 0f, activeContentAlpha, scale);
        }

        if (options.ShowBestBlock)
        {
            DrawBestBlock(drawList, bestBlock, snapshot.BestComboCount, accent, MathF.Max(activeContentAlpha, idleContentAlpha), scale);
        }

        if (idleContentAlpha > 0.01f)
        {
            DrawIdleContent(drawList, layout, canvasOrigin, snapshot, accent, idleContentAlpha, scale, false);
        }
    }

    private static void DrawIdle(StyleMeterSnapshot snapshot, float scale, double animationTimeSeconds, StyleMeterOverlayOptions options)
    {
        var drawList = ImGui.GetWindowDrawList();
        var layout = StyleMeterOverlayLayout.CreateIdle(scale);
        var canvasOrigin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(layout.CanvasSize);

        var panel = layout.Translate(layout.Panel, canvasOrigin);
        var rankMedallion = layout.Translate(layout.RankMedallion, canvasOrigin);
        var pulse = StyleMeterOverlayMath.GetPulseIntensity(animationTimeSeconds, options.AnimationIntensity);
        var alpha = options.Opacity;
        var accent = StyleMeterOverlayPalette.GetRankColor("D", alpha * (0.72f + (pulse * 0.18f)));

        DrawPanel(drawList, panel, accent, StyleMeterOverlayPalette.GetRankColor("D", alpha * 0.22f), alpha * 0.9f, scale);
        DrawRankMedallion(drawList, rankMedallion, "D", accent, alpha * 0.85f, pulse, scale);
        DrawIdleContent(drawList, layout, canvasOrigin, snapshot, accent, alpha * 0.92f, scale, options.ShowBestBlock);
    }

    private static void DrawIdleContent(
        ImDrawListPtr drawList,
        StyleMeterOverlayLayout layout,
        Vector2 canvasOrigin,
        StyleMeterSnapshot snapshot,
        Vector4 accent,
        float alpha,
        float scale,
        bool drawBestBlock = true)
    {
        var statusChip = layout.Translate(layout.StatusChip, canvasOrigin);
        var bestBlock = layout.Translate(layout.BestBlock, canvasOrigin);

        AddText(drawList, canvasOrigin + layout.LabelPosition, StyleMeterOverlayPalette.ToU32(new Vector4(0.72f, 0.78f, 0.9f, alpha * 0.94f)), "STYLE METER", StyleMeterOverlayMath.GetLabelTextSize(scale));
        if (drawBestBlock)
        {
            DrawBestBlock(drawList, bestBlock, snapshot.BestComboCount, accent, alpha, scale);
        }

        DrawStatusChip(drawList, statusChip, "READY", accent, alpha, scale);
    }

    private static void DrawPanel(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect panel,
        Vector4 accent,
        Vector4 accentSoft,
        float alpha,
        float scale)
    {
        var shadowOffset = Scale(new Vector2(3f, 4f), scale);
        var cornerRadius = 11f * scale;
        drawList.AddRectFilled(panel.Min + shadowOffset, panel.Max + shadowOffset, StyleMeterOverlayPalette.ToU32(new Vector4(0f, 0f, 0f, alpha * 0.34f)), cornerRadius);
        drawList.AddRectFilled(panel.Min, panel.Max, StyleMeterOverlayPalette.ToU32(new Vector4(0.024f, 0.027f, 0.043f, alpha * 0.92f)), cornerRadius);
        drawList.AddRectFilled(panel.Min + Scale(new Vector2(1f, 1f), scale), panel.Max - Scale(new Vector2(1f, 1f), scale), StyleMeterOverlayPalette.ToU32(new Vector4(0.065f, 0.07f, 0.105f, alpha * 0.42f)), 9f * scale);
        drawList.AddRect(panel.Min, panel.Max, StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.62f)), cornerRadius, ImDrawFlags.None, 1f * scale);

        var accentLineStartX = panel.Min.X + MathF.Min(86f * scale, panel.Width * 0.42f);
        var accentLineEndX = panel.Max.X - (13f * scale);
        if (accentLineEndX > accentLineStartX)
        {
            drawList.AddLine(
                new Vector2(accentLineStartX, panel.Min.Y + (7f * scale)),
                new Vector2(accentLineEndX, panel.Min.Y + (7f * scale)),
                StyleMeterOverlayPalette.ToU32(accentSoft),
                1.6f * scale);
        }

        for (var i = 0; i < 4; i++)
        {
            var y = panel.Min.Y + ((16f + (i * 9f)) * scale);
            var scanlineStartX = panel.Min.X + MathF.Min(86f * scale, panel.Width * 0.42f);
            var scanlineEndX = panel.Max.X - (13f * scale);
            if (scanlineEndX > scanlineStartX)
            {
                drawList.AddLine(
                    new Vector2(scanlineStartX, y),
                    new Vector2(scanlineEndX, y),
                    StyleMeterOverlayPalette.ToU32(new Vector4(1f, 1f, 1f, alpha * 0.025f)),
                    1f);
            }
        }
    }

    private static void DrawRankMedallion(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect badge,
        string rank,
        Vector4 accent,
        float alpha,
        float pulse,
        float scale)
    {
        DrawRankMedallionBase(drawList, badge, accent, alpha, pulse, scale);
        DrawRankText(drawList, badge, rank, alpha, scale);
    }

    private static void DrawRankMedallionBase(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect badge,
        Vector4 accent,
        float alpha,
        float pulse,
        float scale)
    {
        var badgeFill = StyleMeterOverlayPalette.ToU32(new Vector4(accent.X * 0.15f, accent.Y * 0.15f, accent.Z * 0.15f, alpha * 0.86f));
        var glow = StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * (0.18f + (pulse * 0.12f))));
        var center = badge.Center;
        var radius = MathF.Min(badge.Width, badge.Height) * 0.5f;

        drawList.AddCircleFilled(center, radius + (3f * scale), glow, 28);
        drawList.AddCircleFilled(center, radius, badgeFill, 28);
        drawList.AddCircle(center, radius, StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.95f)), 28, 1.5f * scale);
        var underlineHalfWidth = MathF.Min(18f * scale, radius * 0.62f);
        var underlineOffsetY = MathF.Min(13f * scale, radius * 0.55f);
        drawList.AddLine(
            center + new Vector2(-underlineHalfWidth, underlineOffsetY),
            center + new Vector2(underlineHalfWidth, underlineOffsetY),
            StyleMeterOverlayPalette.ToU32(accent),
            2f * scale);
    }

    private static void DrawRankText(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect badge,
        string rank,
        float alpha,
        float scale)
    {
        var center = badge.Center;
        var fontSize = StyleMeterOverlayMath.GetRankTextSize(rank, scale);
        var textSize = ImGui.CalcTextSize(rank) * (fontSize / ImGui.GetFontSize());
        var rankPosition = center - (textSize * 0.5f) + Scale(new Vector2(0f, -1f), scale);
        AddText(drawList, rankPosition + Scale(new Vector2(1f, 1f), scale), StyleMeterOverlayPalette.ToU32(new Vector4(0f, 0f, 0f, alpha * 0.55f)), rank, fontSize);
        AddText(drawList, rankPosition, StyleMeterOverlayPalette.ToU32(new Vector4(1f, 1f, 1f, alpha)), rank, fontSize);
    }

    private static void DrawMilestoneFlash(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect panel,
        StyleMeterOverlayRect badge,
        Vector4 accent,
        float alpha,
        float flashIntensity,
        float scale)
    {
        if (flashIntensity <= 0)
        {
            return;
        }

        var flashAlpha = alpha * flashIntensity;
        var flashColor = StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, flashAlpha * 0.55f));
        var whiteFlash = StyleMeterOverlayPalette.ToU32(new Vector4(1f, 1f, 1f, flashAlpha * 0.25f));
        var radius = (MathF.Min(badge.Width, badge.Height) * 0.5f) + ((3f + (4f * flashIntensity)) * scale);

        drawList.AddCircle(badge.Center, radius, flashColor, 32, (2.2f + (1.8f * flashIntensity)) * scale);
        drawList.AddRect(panel.Min + Scale(new Vector2(2f, 2f), scale), panel.Max - Scale(new Vector2(2f, 2f), scale), whiteFlash, 9f * scale, ImDrawFlags.None, 1.4f * scale);
    }

    private static void DrawChainText(
        ImDrawListPtr drawList,
        StyleMeterOverlayLayout layout,
        Vector2 canvasOrigin,
        StyleMeterSnapshot snapshot,
        Vector4 accent,
        float alpha,
        float scale,
        bool showChainDetails)
    {
        AddText(drawList, canvasOrigin + layout.LabelPosition, StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.9f)), "STYLE", StyleMeterOverlayMath.GetLabelTextSize(scale));
        AddText(drawList, canvasOrigin + layout.ChainLabelPosition, StyleMeterOverlayPalette.ToU32(new Vector4(0.74f, 0.81f, 0.92f, alpha * 0.82f)), "CHAIN", StyleMeterOverlayMath.GetLabelTextSize(scale));
        DrawComboGlyphs(drawList, canvasOrigin + layout.ComboPosition, StyleMeterOverlayMath.FormatComboCount(snapshot.ComboCount), accent, alpha, scale);
        DrawOutlinedText(
            drawList,
            canvasOrigin + layout.ChainPosition,
            StyleMeterOverlayPalette.ToU32(new Vector4(0.92f, 0.96f, 1f, alpha)),
            StyleMeterOverlayMath.FormatChainCount(StyleMeterOverlayMath.GetDisplayChainCount(snapshot)),
            StyleMeterOverlayMath.GetChainTextSize(scale),
            alpha,
            scale);
        if (showChainDetails)
        {
            AddText(drawList, canvasOrigin + layout.SubLabelPosition, StyleMeterOverlayPalette.ToU32(new Vector4(0.76f, 0.8f, 0.9f, alpha * 0.68f)), StyleMeterOverlayMath.FormatWeaveSummary(snapshot.OffGlobalCooldownCount), StyleMeterOverlayMath.GetSubTextSize(scale));
        }
    }

    private static void DrawBestBlock(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect block,
        int bestComboCount,
        Vector4 accent,
        float alpha,
        float scale)
    {
        if (bestComboCount <= 0 || alpha <= 0)
        {
            return;
        }

        var fill = StyleMeterOverlayPalette.ToU32(new Vector4(accent.X * 0.12f, accent.Y * 0.12f, accent.Z * 0.12f, alpha * 0.84f));
        var border = StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.58f));
        var shine = StyleMeterOverlayPalette.ToU32(new Vector4(1f, 1f, 1f, alpha * 0.07f));
        var cornerRadius = 5f * scale;

        drawList.AddRectFilled(block.Min, block.Max, fill, cornerRadius);
        drawList.AddRectFilled(block.Min + Scale(new Vector2(1f, 1f), scale), new Vector2(block.Max.X, block.Min.Y + (block.Height * 0.5f)), shine, cornerRadius);
        drawList.AddRect(block.Min, block.Max, border, cornerRadius, ImDrawFlags.None, 1f * scale);

        var labelPosition = block.Min + Scale(new Vector2(6f, 4f), scale);
        var valuePosition = block.Min + Scale(new Vector2(42f, 3f), scale);
        AddText(drawList, labelPosition, StyleMeterOverlayPalette.ToU32(new Vector4(0.68f, 0.74f, 0.86f, alpha * 0.86f)), "BEST", StyleMeterOverlayMath.GetBestLabelTextSize(scale));
        DrawOutlinedText(
            drawList,
            valuePosition,
            StyleMeterOverlayPalette.ToU32(new Vector4(1f, 1f, 1f, alpha)),
            StyleMeterOverlayMath.FormatComboCount(bestComboCount),
            StyleMeterOverlayMath.GetBestValueTextSize(scale),
            alpha,
            scale);
    }

    private static void DrawStatusChip(ImDrawListPtr drawList, StyleMeterOverlayRect chip, string text, Vector4 accent, float alpha, float scale)
    {
        drawList.AddRectFilled(chip.Min, chip.Max, StyleMeterOverlayPalette.ToU32(new Vector4(accent.X * 0.16f, accent.Y * 0.16f, accent.Z * 0.16f, alpha * 0.78f)), 5f * scale);
        drawList.AddRect(chip.Min, chip.Max, StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.45f)), 5f * scale, ImDrawFlags.None, 1f * scale);

        var fontSize = StyleMeterOverlayMath.GetStatusTextSize(scale);
        var textSize = ImGui.CalcTextSize(text) * (fontSize / ImGui.GetFontSize());
        AddText(drawList, chip.Center - (textSize * 0.5f), StyleMeterOverlayPalette.ToU32(new Vector4(0.86f, 0.9f, 1f, alpha * 0.9f)), text, fontSize);
    }

    private static void DrawTimerRail(
        ImDrawListPtr drawList,
        StyleMeterOverlayRect rail,
        float progress,
        Vector4 timerColor,
        float dangerIntensity,
        float alpha,
        float scale)
    {
        var safeProgress = Math.Clamp(progress, 0f, 1f);
        var cornerRadius = rail.Height * 0.5f;
        var fillWidth = MathF.Max(rail.Height, rail.Width * safeProgress);
        var fill = new StyleMeterOverlayRect(rail.Min, new Vector2(rail.Min.X + fillWidth, rail.Max.Y));
        var borderAlpha = alpha * (0.18f + (dangerIntensity * 0.42f));

        drawList.AddRectFilled(rail.Min, rail.Max, StyleMeterOverlayPalette.ToU32(new Vector4(0f, 0f, 0f, alpha * 0.44f)), cornerRadius);
        drawList.AddRectFilled(fill.Min, fill.Max, StyleMeterOverlayPalette.ToU32(new Vector4(timerColor.X, timerColor.Y, timerColor.Z, alpha * 0.92f)), cornerRadius);
        drawList.AddRect(rail.Min, rail.Max, StyleMeterOverlayPalette.ToU32(new Vector4(timerColor.X, timerColor.Y, timerColor.Z, borderAlpha)), cornerRadius, ImDrawFlags.None, (1f + dangerIntensity) * scale);

        for (var i = 1; i < 8; i++)
        {
            var x = rail.Min.X + (rail.Width * i / 8f);
            drawList.AddLine(
                new Vector2(x, rail.Min.Y + (2f * scale)),
                new Vector2(x, rail.Max.Y - (2f * scale)),
                StyleMeterOverlayPalette.ToU32(new Vector4(0f, 0f, 0f, alpha * 0.2f)),
                1f);
        }
    }

    private static string GetStatusText(StyleMeterSnapshot snapshot, float dangerIntensity)
    {
        if (!snapshot.IsActive)
        {
            return "ENDED";
        }

        return dangerIntensity > 0 ? "LOW" : "ACTIVE";
    }

    private static void AddText(ImDrawListPtr drawList, Vector2 position, uint color, string text, float fontSize)
    {
        drawList.AddText(ImGui.GetFont(), fontSize, position, color, text);
    }

    private static void DrawOutlinedText(
        ImDrawListPtr drawList,
        Vector2 position,
        uint color,
        string text,
        float fontSize,
        float alpha,
        float scale)
    {
        var outlineColor = StyleMeterOverlayPalette.ToU32(new Vector4(0f, 0f, 0f, alpha * 0.62f));
        var offset = Math.Clamp(1f * scale, 1f, 2f);

        AddText(drawList, position + new Vector2(offset, 0f), outlineColor, text, fontSize);
        AddText(drawList, position + new Vector2(-offset, 0f), outlineColor, text, fontSize);
        AddText(drawList, position + new Vector2(0f, offset), outlineColor, text, fontSize);
        AddText(drawList, position + new Vector2(0f, -offset), outlineColor, text, fontSize);
        AddText(drawList, position, color, text, fontSize);
    }

    private static void DrawComboGlyphs(
        ImDrawListPtr drawList,
        Vector2 position,
        string text,
        Vector4 accent,
        float alpha,
        float scale)
    {
        var height = StyleMeterOverlayMath.GetComboGlyphHeight(scale);
        var stroke = Math.Clamp(height * 0.095f, 1.8f, 3.4f);
        var gap = 3f * StyleMeterOverlayMath.NormalizeOverlayScale(scale);
        var cursor = position;
        var shadowOffset = new Vector2(Math.Clamp(1.4f * scale, 1f, 2.5f));
        var shadowColor = StyleMeterOverlayPalette.ToU32(new Vector4(0f, 0f, 0f, alpha * 0.68f));
        var glowColor = StyleMeterOverlayPalette.ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.16f));
        var mainColor = StyleMeterOverlayPalette.ToU32(new Vector4(1f, 1f, 1f, alpha));

        foreach (var character in text)
        {
            var width = StyleMeterOverlayMath.GetComboGlyphWidth(character, height);
            DrawComboGlyph(drawList, cursor + shadowOffset, character, width, height, stroke + 1f, shadowColor);
            DrawComboGlyph(drawList, cursor, character, width, height, stroke + 2.5f, glowColor);
            DrawComboGlyph(drawList, cursor, character, width, height, stroke, mainColor);
            cursor.X += width + gap;
        }
    }

    private static void DrawComboGlyph(ImDrawListPtr drawList, Vector2 origin, char character, float width, float height, float stroke, uint color)
    {
        switch (character)
        {
            case 'x':
            case 'X':
                DrawLineGlyph(drawList, origin + new Vector2(stroke, height * 0.24f), origin + new Vector2(width - stroke, height * 0.8f), stroke, color);
                DrawLineGlyph(drawList, origin + new Vector2(width - stroke, height * 0.24f), origin + new Vector2(stroke, height * 0.8f), stroke, color);
                break;
            case '+':
                DrawLineGlyph(drawList, origin + new Vector2(width * 0.18f, height * 0.55f), origin + new Vector2(width * 0.82f, height * 0.55f), stroke, color);
                DrawLineGlyph(drawList, origin + new Vector2(width * 0.5f, height * 0.25f), origin + new Vector2(width * 0.5f, height * 0.85f), stroke, color);
                break;
            case >= '0' and <= '9':
                DrawSevenSegmentDigit(drawList, origin, character, width, height, stroke, color);
                break;
        }
    }

    private static void DrawSevenSegmentDigit(ImDrawListPtr drawList, Vector2 origin, char digit, float width, float height, float stroke, uint color)
    {
        var segments = digit switch
        {
            '0' => (Top: true, UpperLeft: true, UpperRight: true, Middle: false, LowerLeft: true, LowerRight: true, Bottom: true),
            '1' => (Top: false, UpperLeft: false, UpperRight: true, Middle: false, LowerLeft: false, LowerRight: true, Bottom: false),
            '2' => (Top: true, UpperLeft: false, UpperRight: true, Middle: true, LowerLeft: true, LowerRight: false, Bottom: true),
            '3' => (Top: true, UpperLeft: false, UpperRight: true, Middle: true, LowerLeft: false, LowerRight: true, Bottom: true),
            '4' => (Top: false, UpperLeft: true, UpperRight: true, Middle: true, LowerLeft: false, LowerRight: true, Bottom: false),
            '5' => (Top: true, UpperLeft: true, UpperRight: false, Middle: true, LowerLeft: false, LowerRight: true, Bottom: true),
            '6' => (Top: true, UpperLeft: true, UpperRight: false, Middle: true, LowerLeft: true, LowerRight: true, Bottom: true),
            '7' => (Top: true, UpperLeft: false, UpperRight: true, Middle: false, LowerLeft: false, LowerRight: true, Bottom: false),
            '8' => (Top: true, UpperLeft: true, UpperRight: true, Middle: true, LowerLeft: true, LowerRight: true, Bottom: true),
            _ => (Top: true, UpperLeft: true, UpperRight: true, Middle: true, LowerLeft: false, LowerRight: true, Bottom: true),
        };

        var left = origin.X + (stroke * 0.55f);
        var right = origin.X + width - (stroke * 0.55f);
        var top = origin.Y + (stroke * 0.55f);
        var middle = origin.Y + (height * 0.52f);
        var bottom = origin.Y + height - (stroke * 0.55f);
        var upperStart = origin.Y + (stroke * 1.2f);
        var upperEnd = middle - (stroke * 0.8f);
        var lowerStart = middle + (stroke * 0.8f);
        var lowerEnd = origin.Y + height - (stroke * 1.2f);

        if (segments.Top)
        {
            DrawLineGlyph(drawList, new Vector2(left, top), new Vector2(right, top), stroke, color);
        }

        if (segments.UpperLeft)
        {
            DrawLineGlyph(drawList, new Vector2(left, upperStart), new Vector2(left, upperEnd), stroke, color);
        }

        if (segments.UpperRight)
        {
            DrawLineGlyph(drawList, new Vector2(right, upperStart), new Vector2(right, upperEnd), stroke, color);
        }

        if (segments.Middle)
        {
            DrawLineGlyph(drawList, new Vector2(left, middle), new Vector2(right, middle), stroke, color);
        }

        if (segments.LowerLeft)
        {
            DrawLineGlyph(drawList, new Vector2(left, lowerStart), new Vector2(left, lowerEnd), stroke, color);
        }

        if (segments.LowerRight)
        {
            DrawLineGlyph(drawList, new Vector2(right, lowerStart), new Vector2(right, lowerEnd), stroke, color);
        }

        if (segments.Bottom)
        {
            DrawLineGlyph(drawList, new Vector2(left, bottom), new Vector2(right, bottom), stroke, color);
        }
    }

    private static void DrawLineGlyph(ImDrawListPtr drawList, Vector2 start, Vector2 end, float stroke, uint color)
    {
        drawList.AddLine(start, end, color, stroke);
    }

    private static Vector2 Scale(Vector2 vector, float scale)
    {
        return vector * scale;
    }
}

internal readonly record struct StyleMeterOverlayRenderState(float MilestoneFlashIntensity)
{
    public static StyleMeterOverlayRenderState None => default;
}

internal readonly record struct StyleMeterOverlayOptions(
    float Opacity,
    float AnimationIntensity,
    bool ShowBestBlock,
    bool ShowChainDetails)
{
    public static StyleMeterOverlayOptions Default => new(1f, 1f, true, true);

    public static StyleMeterOverlayOptions FromConfiguration(Configuration configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return Normalize(new StyleMeterOverlayOptions(
            configuration.OverlayOpacity,
            configuration.AnimationIntensity,
            configuration.ShowBestBlock,
            configuration.ShowChainDetails));
    }

    public static StyleMeterOverlayOptions Normalize(StyleMeterOverlayOptions options)
    {
        return new StyleMeterOverlayOptions(
            StyleMeterOverlayMath.NormalizeOverlayOpacity(options.Opacity),
            StyleMeterOverlayMath.NormalizeAnimationIntensity(options.AnimationIntensity),
            options.ShowBestBlock,
            options.ShowChainDetails);
    }
}

internal readonly record struct StyleMeterOverlayVisibility(bool ShouldDraw, bool UsePreviewSnapshot)
{
    public static StyleMeterOverlayVisibility Resolve(
        bool showOverlay,
        bool autoHideOutsideCombat,
        bool isInCombat,
        bool previewCombatOverlay)
    {
        if (previewCombatOverlay)
        {
            return new StyleMeterOverlayVisibility(true, true);
        }

        if (!showOverlay)
        {
            return new StyleMeterOverlayVisibility(false, false);
        }

        if (autoHideOutsideCombat && !isInCombat)
        {
            return new StyleMeterOverlayVisibility(false, false);
        }

        return new StyleMeterOverlayVisibility(true, false);
    }
}

internal static class StyleMeterOverlayWindowFlags
{
    public static readonly ImGuiWindowFlags Base =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoSavedSettings;

    public static ImGuiWindowFlags Create(bool locked, bool clickThrough)
    {
        return Create(StyleMeterOverlayInputMode.Resolve(locked, clickThrough));
    }

    public static ImGuiWindowFlags Create(StyleMeterOverlayInputMode inputMode)
    {
        var flags = Base;
        if (inputMode.LocksMovement)
        {
            flags |= ImGuiWindowFlags.NoMove;
        }

        if (inputMode.UsesNoInputs)
        {
            flags |= ImGuiWindowFlags.NoInputs;
        }

        return flags;
    }
}

internal readonly record struct StyleMeterOverlayInputMode(bool LocksMovement, bool UsesNoInputs)
{
    public static StyleMeterOverlayInputMode Resolve(bool locked, bool clickThrough)
    {
        return Resolve(locked, clickThrough, forceInteractive: false);
    }

    public static StyleMeterOverlayInputMode Resolve(bool locked, bool clickThrough, bool forceInteractive)
    {
        return new StyleMeterOverlayInputMode(
            !forceInteractive && (locked || clickThrough),
            !forceInteractive && clickThrough);
    }
}

internal static class StyleMeterPreviewSnapshot
{
    public static StyleMeterSnapshot CreateCombat(DateTime nowUtc)
    {
        return new StyleMeterSnapshot(
            50,
            "S",
            true,
            false,
            2.5f,
            0.5f,
            nowUtc.AddSeconds(-1.2),
            nowUtc.AddSeconds(1.8),
            DateTime.MinValue,
            8,
            58,
            74);
    }
}

internal static class StyleMeterOverlayAnchor
{
    public static float GetTopCenterAnchorOffsetX(StyleMeterSnapshot snapshot, DateTime nowUtc, float scale)
    {
        var safeScale = StyleMeterOverlayMath.NormalizeOverlayScale(scale);
        var activeLayout = StyleMeterOverlayLayout.CreateActive(safeScale);
        var currentLayout = GetCurrentLayout(snapshot, nowUtc, safeScale);

        return GetTopCenterAnchorOffsetX(activeLayout.CanvasSize.X, currentLayout.CanvasSize.X);
    }

    public static float GetTopCenterAnchorOffsetX(float activeWidth, float currentWidth)
    {
        if (!IsFinite(activeWidth) || !IsFinite(currentWidth))
        {
            return 0f;
        }

        return MathF.Max(0f, (activeWidth - currentWidth) * 0.5f);
    }

    private static StyleMeterOverlayLayout GetCurrentLayout(StyleMeterSnapshot snapshot, DateTime nowUtc, float scale)
    {
        if (StyleMeterOverlayMath.ShouldDrawIdle(snapshot))
        {
            return StyleMeterOverlayLayout.CreateIdle(scale);
        }

        if (snapshot.IsFading)
        {
            return StyleMeterOverlayLayout.CreateTransition(
                scale,
                StyleMeterOverlayMath.GetEndTransitionProgress(snapshot, nowUtc));
        }

        return StyleMeterOverlayLayout.CreateActive(scale);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}

internal sealed class StyleMeterOverlayAnimationState
{
    private int lastComboCount;
    private string lastRank = "D";
    private double lastMilestoneTriggerTimeSeconds = double.NegativeInfinity;

    public StyleMeterOverlayRenderState Update(StyleMeterSnapshot snapshot, double animationTimeSeconds)
    {
        var safeTime = StyleMeterOverlayMath.NormalizeAnimationTime(animationTimeSeconds);

        if (snapshot.ComboCount <= 0)
        {
            this.lastComboCount = 0;
            this.lastRank = snapshot.Rank ?? "D";
            this.lastMilestoneTriggerTimeSeconds = double.NegativeInfinity;
            return StyleMeterOverlayRenderState.None;
        }

        if (StyleMeterOverlayMath.ShouldTriggerMilestoneFlash(this.lastComboCount, snapshot.ComboCount))
        {
            this.lastMilestoneTriggerTimeSeconds = safeTime;
        }

        this.lastComboCount = snapshot.ComboCount;
        this.lastRank = snapshot.Rank ?? "D";

        return new StyleMeterOverlayRenderState(
            StyleMeterOverlayMath.GetMilestoneFlashIntensity(this.lastMilestoneTriggerTimeSeconds, safeTime));
    }
}

internal readonly record struct StyleMeterOverlayRect(Vector2 Min, Vector2 Max)
{
    public Vector2 Size => this.Max - this.Min;

    public float Width => this.Max.X - this.Min.X;

    public float Height => this.Max.Y - this.Min.Y;

    public Vector2 Center => (this.Min + this.Max) * 0.5f;

    public StyleMeterOverlayRect Translate(Vector2 offset)
    {
        return new StyleMeterOverlayRect(this.Min + offset, this.Max + offset);
    }

    public bool Contains(StyleMeterOverlayRect rect)
    {
        return rect.Min.X >= this.Min.X &&
               rect.Min.Y >= this.Min.Y &&
               rect.Max.X <= this.Max.X &&
               rect.Max.Y <= this.Max.Y;
    }
}

internal readonly record struct StyleMeterOverlayLayout(
    float Scale,
    Vector2 CanvasSize,
    StyleMeterOverlayRect Canvas,
    StyleMeterOverlayRect Panel,
    StyleMeterOverlayRect RankMedallion,
    StyleMeterOverlayRect StatusChip,
    StyleMeterOverlayRect BestBlock,
    StyleMeterOverlayRect TimerRail,
    Vector2 LabelPosition,
    Vector2 ComboPosition,
    Vector2 ChainLabelPosition,
    Vector2 ChainPosition,
    Vector2 SubLabelPosition)
{
    public const float ActivePanelWidth = 332f;
    public const float ActivePanelHeight = 78f;
    public const float IdlePanelWidth = 218f;
    public const float IdlePanelHeight = 52f;
    public const float Bleed = 8f;

    public static StyleMeterOverlayLayout CreateActive(float scale)
    {
        var safeScale = StyleMeterOverlayMath.NormalizeOverlayScale(scale);
        var bleed = Bleed * safeScale;
        var panelMin = new Vector2(bleed, bleed);
        var panelSize = new Vector2(ActivePanelWidth, ActivePanelHeight) * safeScale;
        var panel = new StyleMeterOverlayRect(panelMin, panelMin + panelSize);

        return new StyleMeterOverlayLayout(
            safeScale,
            panelSize + new Vector2(bleed * 2f),
            new StyleMeterOverlayRect(Vector2.Zero, panelSize + new Vector2(bleed * 2f)),
            panel,
            CreateRect(panelMin, new Vector2(12f, 8f), new Vector2(52f, 52f), safeScale),
            CreateRect(panelMin, new Vector2(264f, 13f), new Vector2(54f, 18f), safeScale),
            CreateRect(panelMin, new Vector2(218f, 39f), new Vector2(100f, 20f), safeScale),
            CreateRect(panelMin, new Vector2(82f, 68f), new Vector2(236f, 6f), safeScale),
            panelMin + (new Vector2(82f, 12f) * safeScale),
            panelMin + (new Vector2(82f, 28f) * safeScale),
            panelMin + (new Vector2(178f, 12f) * safeScale),
            panelMin + (new Vector2(178f, 28f) * safeScale),
            panelMin + (new Vector2(178f, 49f) * safeScale));
    }

    public static StyleMeterOverlayLayout CreateIdle(float scale)
    {
        var safeScale = StyleMeterOverlayMath.NormalizeOverlayScale(scale);
        var bleed = Bleed * safeScale;
        var panelMin = new Vector2(bleed, bleed);
        var panelSize = new Vector2(IdlePanelWidth, IdlePanelHeight) * safeScale;
        var panel = new StyleMeterOverlayRect(panelMin, panelMin + panelSize);

        return new StyleMeterOverlayLayout(
            safeScale,
            panelSize + new Vector2(bleed * 2f),
            new StyleMeterOverlayRect(Vector2.Zero, panelSize + new Vector2(bleed * 2f)),
            panel,
            CreateRect(panelMin, new Vector2(10f, 9f), new Vector2(34f, 34f), safeScale),
            CreateRect(panelMin, new Vector2(152f, 13f), new Vector2(52f, 18f), safeScale),
            CreateRect(panelMin, new Vector2(50f, 28f), new Vector2(86f, 16f), safeScale),
            CreateRect(panelMin, new Vector2(50f, 44f), new Vector2(154f, 4f), safeScale),
            panelMin + (new Vector2(50f, 10f) * safeScale),
            panelMin + (new Vector2(50f, 23f) * safeScale),
            panelMin + (new Vector2(0f, 0f) * safeScale),
            panelMin + (new Vector2(0f, 0f) * safeScale),
            panelMin + (new Vector2(0f, 0f) * safeScale));
    }

    public static StyleMeterOverlayLayout CreateTransition(float scale, float progress)
    {
        var active = CreateActive(scale);
        var idle = CreateIdle(scale);
        var amount = StyleMeterOverlayMath.GetEndLayoutProgress(progress);
        var canvasSize = Lerp(active.CanvasSize, idle.CanvasSize, amount);

        return new StyleMeterOverlayLayout(
            active.Scale,
            canvasSize,
            new StyleMeterOverlayRect(Vector2.Zero, canvasSize),
            Lerp(active.Panel, idle.Panel, amount),
            Lerp(active.RankMedallion, idle.RankMedallion, amount),
            Lerp(active.StatusChip, idle.StatusChip, amount),
            Lerp(active.BestBlock, idle.BestBlock, amount),
            Lerp(active.TimerRail, idle.TimerRail, amount),
            Lerp(active.LabelPosition, idle.LabelPosition, amount),
            Lerp(active.ComboPosition, idle.ComboPosition, amount),
            Lerp(active.ChainLabelPosition, idle.ChainLabelPosition, amount),
            Lerp(active.ChainPosition, idle.ChainPosition, amount),
            Lerp(active.SubLabelPosition, idle.SubLabelPosition, amount));
    }

    public StyleMeterOverlayRect Translate(StyleMeterOverlayRect rect, Vector2 origin)
    {
        return rect.Translate(origin);
    }

    public bool ContainsDrawableBounds()
    {
        return this.Canvas.Contains(this.Panel) &&
               this.Canvas.Contains(this.RankMedallion) &&
               this.Canvas.Contains(this.StatusChip) &&
               this.Canvas.Contains(this.BestBlock) &&
               this.Canvas.Contains(this.TimerRail);
    }

    private static StyleMeterOverlayRect CreateRect(Vector2 panelMin, Vector2 offset, Vector2 size, float scale)
    {
        var min = panelMin + (offset * scale);
        return new StyleMeterOverlayRect(min, min + (size * scale));
    }

    private static StyleMeterOverlayRect Lerp(StyleMeterOverlayRect from, StyleMeterOverlayRect to, float amount)
    {
        return new StyleMeterOverlayRect(
            Lerp(from.Min, to.Min, amount),
            Lerp(from.Max, to.Max, amount));
    }

    private static Vector2 Lerp(Vector2 from, Vector2 to, float amount)
    {
        return from + ((to - from) * amount);
    }
}

internal static class StyleMeterOverlayPalette
{
    private static readonly Vector4 TimerDangerColor = new(1f, 0.22f, 0.08f, 1f);
    private static readonly Vector4 TimerWarningColor = new(1f, 0.72f, 0.12f, 1f);

    public static Vector4 GetRankColor(string rank, float alpha)
    {
        var safeAlpha = Clamp01(alpha, 1f);
        return rank switch
        {
            "SSS" => new Vector4(1f, 0.24f, 0.86f, safeAlpha),
            "SS" => new Vector4(0.72f, 0.36f, 1f, safeAlpha),
            "S" => new Vector4(1f, 0.82f, 0.18f, safeAlpha),
            "A" => new Vector4(0.22f, 0.86f, 1f, safeAlpha),
            "B" => new Vector4(0.25f, 1f, 0.42f, safeAlpha),
            "C" => new Vector4(0.62f, 0.84f, 1f, safeAlpha),
            _ => new Vector4(0.92f, 0.95f, 1f, safeAlpha),
        };
    }

    public static Vector4 GetTimerColor(Vector4 rankColor, float dangerIntensity, float alpha)
    {
        var safeDangerIntensity = StyleMeterOverlayMath.NormalizeUnit(dangerIntensity);
        var warningBlend = Blend(rankColor, TimerWarningColor with { W = alpha }, safeDangerIntensity * 0.75f);
        return Blend(warningBlend, TimerDangerColor with { W = alpha }, safeDangerIntensity);
    }

    public static Vector4 Blend(Vector4 from, Vector4 to, float amount)
    {
        var safeAmount = StyleMeterOverlayMath.NormalizeUnit(amount);
        return new Vector4(
            Lerp(from.X, to.X, safeAmount),
            Lerp(from.Y, to.Y, safeAmount),
            Lerp(from.Z, to.Z, safeAmount),
            Lerp(from.W, to.W, safeAmount));
    }

    public static uint ToU32(Vector4 color)
    {
        var r = (uint)(Clamp01(color.X, 0f) * 255f);
        var g = (uint)(Clamp01(color.Y, 0f) * 255f);
        var b = (uint)(Clamp01(color.Z, 0f) * 255f);
        var a = (uint)(Clamp01(color.W, 0f) * 255f);

        return r | (g << 8) | (b << 16) | (a << 24);
    }

    private static float Clamp01(float value, float fallback)
    {
        return float.IsNaN(value) ? fallback : Math.Clamp(value, 0f, 1f);
    }

    private static float Lerp(float start, float end, float amount)
    {
        return start + ((end - start) * amount);
    }
}

internal static class StyleMeterOverlayMath
{
    public const float TimerDangerThreshold = 0.22f;
    public const double MilestoneFlashDurationSeconds = 0.45;

    private static readonly int[] MilestoneComboCounts = [8, 16, 25, 50, 100, 152];

    public static float NormalizeOverlayScale(float scale)
    {
        if (float.IsNaN(scale))
        {
            return 1f;
        }

        if (float.IsNegativeInfinity(scale))
        {
            return 0.75f;
        }

        if (float.IsPositiveInfinity(scale))
        {
            return 2.5f;
        }

        return Math.Clamp(scale, 0.75f, 2.5f);
    }

    public static float NormalizeOverlayOpacity(float opacity)
    {
        if (float.IsNaN(opacity))
        {
            return 1f;
        }

        if (float.IsNegativeInfinity(opacity))
        {
            return 0.2f;
        }

        if (float.IsPositiveInfinity(opacity))
        {
            return 1f;
        }

        return Math.Clamp(opacity, 0.2f, 1f);
    }

    public static float NormalizeAnimationIntensity(float intensity)
    {
        if (float.IsNaN(intensity))
        {
            return 1f;
        }

        if (float.IsNegativeInfinity(intensity))
        {
            return 0f;
        }

        if (float.IsPositiveInfinity(intensity))
        {
            return 1.5f;
        }

        return Math.Clamp(intensity, 0f, 1.5f);
    }

    public static float GetTimerProgress(StyleMeterSnapshot snapshot, DateTime nowUtc)
    {
        if (!snapshot.IsActive || !IsUsableDuration(snapshot.CurrentRecastSeconds))
        {
            return 0f;
        }

        var totalSeconds = snapshot.CurrentRecastSeconds + snapshot.GraceThresholdSeconds;
        if (!IsUsableDuration(totalSeconds))
        {
            return 0f;
        }

        var remainingSeconds = Math.Max(0, (snapshot.ExpirationTimeUtc - nowUtc).TotalSeconds);
        return Math.Clamp((float)(remainingSeconds / totalSeconds), 0f, 1f);
    }

    public static float GetFadeAlpha(StyleMeterSnapshot snapshot, DateTime nowUtc)
    {
        var elapsedSeconds = (nowUtc - snapshot.LastEndedTimeUtc).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return 1f;
        }

        return Math.Clamp(1f - (float)(elapsedSeconds / StyleMeterComboEngine.FadeDurationSeconds), 0f, 1f);
    }

    public static float GetEndTransitionProgress(StyleMeterSnapshot snapshot, DateTime nowUtc)
    {
        if (snapshot.LastEndedTimeUtc == DateTime.MinValue)
        {
            return 0f;
        }

        var elapsedSeconds = (nowUtc - snapshot.LastEndedTimeUtc).TotalSeconds;
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0)
        {
            return 0f;
        }

        return NormalizeUnit((float)(elapsedSeconds / StyleMeterComboEngine.FadeDurationSeconds));
    }

    public static float GetEndLayoutProgress(float transitionProgress)
    {
        return SmoothStep(NormalizeUnit(transitionProgress));
    }

    public static float GetEndingActiveContentAlpha(float transitionProgress)
    {
        return 1f - SmoothStep(0.18f, 0.72f, transitionProgress);
    }

    public static float GetEndingIdleContentAlpha(float transitionProgress)
    {
        return SmoothStep(0.55f, 0.95f, transitionProgress);
    }

    public static float GetPulseIntensity(double animationTimeSeconds)
    {
        if (double.IsNaN(animationTimeSeconds) || double.IsInfinity(animationTimeSeconds))
        {
            return 0.5f;
        }

        return Math.Clamp(0.5f + (0.5f * (float)Math.Sin(animationTimeSeconds * 4.2)), 0f, 1f);
    }

    public static float GetPulseIntensity(double animationTimeSeconds, float animationIntensity)
    {
        var basePulse = GetPulseIntensity(animationTimeSeconds);
        var safeIntensity = NormalizeAnimationIntensity(animationIntensity);
        return NormalizeUnit(0.5f + ((basePulse - 0.5f) * safeIntensity));
    }

    public static bool IsTimerDanger(float progress)
    {
        return IsFinite(progress) &&
               progress > 0 &&
               progress <= TimerDangerThreshold;
    }

    public static float GetTimerDangerIntensity(float progress, float pulse)
    {
        if (!IsTimerDanger(progress))
        {
            return 0f;
        }

        var urgency = 1f - Math.Clamp(progress / TimerDangerThreshold, 0f, 1f);
        return NormalizeUnit(0.45f + (urgency * 0.4f) + (NormalizeUnit(pulse) * 0.15f));
    }

    public static bool IsMilestoneComboCount(int comboCount)
    {
        return Array.IndexOf(MilestoneComboCounts, comboCount) >= 0;
    }

    public static bool ShouldTriggerMilestoneFlash(int previousComboCount, int currentComboCount)
    {
        return currentComboCount > previousComboCount &&
               IsMilestoneComboCount(currentComboCount);
    }

    public static double NormalizeAnimationTime(double animationTimeSeconds)
    {
        return double.IsNaN(animationTimeSeconds) || double.IsInfinity(animationTimeSeconds)
            ? 0
            : Math.Max(0, animationTimeSeconds);
    }

    public static float GetMilestoneFlashIntensity(double triggerTimeSeconds, double animationTimeSeconds)
    {
        var safeTime = NormalizeAnimationTime(animationTimeSeconds);
        if (double.IsNegativeInfinity(triggerTimeSeconds) || double.IsNaN(triggerTimeSeconds) || triggerTimeSeconds > safeTime)
        {
            return 0f;
        }

        var elapsed = safeTime - triggerTimeSeconds;
        if (elapsed >= MilestoneFlashDurationSeconds - 0.000001)
        {
            return 0f;
        }

        return NormalizeUnit((float)(1 - (elapsed / MilestoneFlashDurationSeconds)));
    }

    public static float NormalizeUnit(float value)
    {
        if (float.IsNaN(value) || float.IsNegativeInfinity(value))
        {
            return 0f;
        }

        if (float.IsPositiveInfinity(value))
        {
            return 1f;
        }

        return Math.Clamp(value, 0f, 1f);
    }

    public static string FormatComboCount(int comboCount)
    {
        return comboCount >= 1000 ? "x999+" : $"x{Math.Max(0, comboCount)}";
    }

    public static string FormatChainCount(int chainCount)
    {
        return chainCount >= 1000 ? "x999+" : $"x{Math.Max(0, chainCount)}";
    }

    public static string FormatWeaveCount(int offGlobalCooldownCount)
    {
        return offGlobalCooldownCount <= 0
            ? "GCD ONLY"
            : $"+{Math.Min(offGlobalCooldownCount, 999)} WEAVE";
    }

    public static string FormatWeaveSummary(int offGlobalCooldownCount)
    {
        return $"W{Math.Clamp(offGlobalCooldownCount, 0, 999)}";
    }

    public static string FormatBestCount(int bestComboCount)
    {
        return $"BEST {FormatComboCount(bestComboCount)}";
    }

    public static string FormatChainDetail(int offGlobalCooldownCount, int bestComboCount)
    {
        var safeWeaveCount = Math.Clamp(offGlobalCooldownCount, 0, 999);
        return bestComboCount > 0
            ? $"W{safeWeaveCount} / {FormatBestCount(bestComboCount)}"
            : FormatWeaveCount(offGlobalCooldownCount);
    }

    public static int GetDisplayChainCount(StyleMeterSnapshot snapshot)
    {
        var derivedChainCount = Math.Max(0, snapshot.ComboCount) + Math.Max(0, snapshot.OffGlobalCooldownCount);
        return Math.Max(derivedChainCount, Math.Max(0, snapshot.ChainCount));
    }

    public static bool ShouldDrawIdle(StyleMeterSnapshot snapshot)
    {
        return snapshot.ComboCount <= 0 ||
               (!snapshot.IsActive && !snapshot.IsFading);
    }

    public static float GetComboTextSize(float scale)
    {
        return GetComboGlyphHeight(scale);
    }

    public static float GetComboGlyphHeight(float scale)
    {
        return GetReadableTextSize(24f, scale, 20f, 32f);
    }

    public static float GetComboGlyphWidth(char character, float glyphHeight)
    {
        var safeHeight = float.IsNaN(glyphHeight) || float.IsInfinity(glyphHeight)
            ? 24f
            : Math.Clamp(glyphHeight, 1f, 64f);

        return character switch
        {
            'x' or 'X' or '+' => safeHeight * 0.42f,
            >= '0' and <= '9' => safeHeight * 0.52f,
            _ => safeHeight * 0.36f,
        };
    }

    public static Vector2 EstimateComboGlyphSize(string text, float scale)
    {
        var safeText = text ?? string.Empty;
        var safeScale = NormalizeOverlayScale(scale);
        var height = GetComboGlyphHeight(safeScale);
        var gap = 3f * safeScale;
        var width = 0f;

        for (var i = 0; i < safeText.Length; i++)
        {
            width += GetComboGlyphWidth(safeText[i], height);
            if (i < safeText.Length - 1)
            {
                width += gap;
            }
        }

        return new Vector2(width, height);
    }

    public static float GetChainTextSize(float scale)
    {
        return GetReadableTextSize(15f, scale, 12f, 21f);
    }

    public static float GetBestLabelTextSize(float scale)
    {
        return GetReadableTextSize(8.5f, scale, 8f, 12f);
    }

    public static float GetBestValueTextSize(float scale)
    {
        return GetReadableTextSize(13f, scale, 11f, 18f);
    }

    public static float GetRankTextSize(string rank, float scale)
    {
        var baseSize = rank.Length >= 3 ? 14f : rank.Length == 2 ? 16f : 19f;
        return GetReadableTextSize(baseSize, scale, 13f, 24f);
    }

    public static float GetLabelTextSize(float scale)
    {
        return GetReadableTextSize(12f, scale, 10f, 18f);
    }

    public static float GetStatusTextSize(float scale)
    {
        return GetReadableTextSize(9.5f, scale, 9f, 15f);
    }

    public static float GetSubTextSize(float scale)
    {
        return GetReadableTextSize(10.5f, scale, 9f, 16f);
    }

    public static float GetSafeTextSize(float baseSize, float scale)
    {
        return GetReadableTextSize(baseSize, scale, 8f, 30f);
    }

    private static float GetReadableTextSize(float baseSize, float scale, float minSize, float maxSize)
    {
        var safeScale = NormalizeOverlayScale(scale);
        return Math.Clamp(baseSize * MathF.Sqrt(safeScale), minSize, maxSize);
    }

    private static float SmoothStep(float value)
    {
        var safeValue = NormalizeUnit(value);
        return safeValue * safeValue * (3f - (2f * safeValue));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (float.IsNaN(edge0) || float.IsNaN(edge1) || edge1 <= edge0)
        {
            return SmoothStep(value);
        }

        var normalized = NormalizeUnit((value - edge0) / (edge1 - edge0));
        return SmoothStep(normalized);
    }

    public static Vector2 EstimateTextSize(string text, float fontSize)
    {
        var safeText = text ?? string.Empty;
        var safeFontSize = float.IsNaN(fontSize) || float.IsInfinity(fontSize)
            ? 12f
            : Math.Clamp(fontSize, 1f, 64f);

        return new Vector2(safeText.Length * safeFontSize * 0.58f, safeFontSize);
    }

    private static bool IsUsableDuration(float durationSeconds)
    {
        return durationSeconds > 0 &&
               IsFinite(durationSeconds);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}
