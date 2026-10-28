using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Desktop;
using IRIS.Desktop.Animation;
using IRIS.Infrastructure;

namespace IRIS.DesktopTests;

internal static class AnimationChecks
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static T Find<T>(Control control, string name) where T : Control => (T)control.Controls.Find(name, true).Single();
    private static async Task UntilAsync(Func<bool> condition, int milliseconds = 10000)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition()) { if (elapsed.ElapsedMilliseconds > milliseconds) throw new TimeoutException("Animation test timed out"); await Task.Delay(10); }
    }
    private static void Render(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, ImageFormat.Png);
    }
    public static async Task<IReadOnlyList<string>> RunAsync(Form owner, string output, string data)
    {
        var results = new List<string>();
        string engineFixture = Path.Combine(data, "portable-engine-test");
        string appFixture = Path.Combine(engineFixture, "app"); Directory.CreateDirectory(appFixture);
        string sharedFixture = Path.Combine(engineFixture, "IRIS-engine-data");
        string emptyUser = Path.Combine(data, "empty-user"); Directory.CreateDirectory(emptyUser);
        foreach (string file in EngineAssets.ChatFiles.Concat(EngineAssets.SpeechFiles))
        {
            string path = Path.Combine(sharedFixture, file); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, new byte[] { 0 });
        }
        Require(EngineAssets.Resolve(appFixture, emptyUser) == sharedFixture && EngineAssets.Missing(sharedFixture, false).Count == 0, "Portable engines depend on user cache");
        Require(EngineAssets.Resolve(appFixture + Path.DirectorySeparatorChar, emptyUser) == sharedFixture, "AppContext trailing separator changed portable resolution");
        Require(EngineAssets.Missing(emptyUser, false).Count == 3, "Missing engines not diagnosed");
        results.Add("portable resource resolution finds shared engines with an empty user profile and reports missing files");
        var catalog = new AnimationCatalog(Path.Combine(AppContext.BaseDirectory, "assets"));
        Require(catalog.Animations.Count == 25 && catalog.Animations.All(a => a.Error == null), "Actual IRIS catalog invalid: " + string.Join("; ", catalog.Animations.Where(a => a.Error != null).Select(a => a.Name + ": " + a.Error)));
        Require(catalog.Notices.Any(n => n.Contains("résolu", StringComparison.Ordinal)), "Manifest path fallback was silent");
        long largest = 0;
        foreach (AnimationDefinition definition in catalog.Animations)
        {
            using SpriteFrames clip = SpriteFrames.Load(definition, 256, default);
            Require(clip.Frames.Length == definition.Frames && clip.Frames.All(f => f.Size == new Size(171, 256)), "Frame count or proportions changed");
            Require(clip.Frames.All(f => f.GetPixel(0, 0).A == 0 && f.GetPixel(f.Width - 1, f.Height - 1).A == 0), "Transparency lost");
            Require(clip.Frames.All(f => f.PixelFormat == PixelFormat.Format32bppPArgb), "Premultiplied alpha lost");
            largest = Math.Max(largest, clip.RetainedBytes);
            if (definition.Name is "idle" or "talk" or "mouth" or "walk") clip.Frames[Math.Min(1, clip.Frames.Length - 1)].Save(Path.Combine(output, "animation-" + definition.Name + ".png"), ImageFormat.Png);
            if (definition.Name == "idle")
            {
                bool partialAlpha = false;
                for (int y = 0; y < 256; y++) for (int x = 0; x < 171; x++) { byte alpha = clip.Frames[0].GetPixel(x, y).A; partialAlpha |= alpha > 0 && alpha < 255; }
                Require(partialAlpha, "Antialiased alpha edges were flattened");
            }
        }
        Require(largest < 5 * 1048576, "Full-size sheets retained instead of reduced frames");
        results.Add("all 25 IRIS sheets decode lazily with original proportions, partial alpha and bounded reduced frames");

        var once = new AnimationTimeline(4, 8, false); var looping = new AnimationTimeline(4, 8, true);
        Require(once.FrameAt(0) == 0 && once.FrameAt(0.125) == 1 && once.FrameAt(20) == 3 && once.CompleteAt(0.5) && !once.CompleteAt(0.499), "One-shot FPS timing incorrect");
        Require(looping.FrameAt(0.5) == 0 && looping.FrameAt(1.375) == 3 && !looping.CompleteAt(100), "Loop timeline incorrect");
        results.Add("elapsed-time animation respects FPS, skips delayed frames and freezes non-looping clips at the last frame");

        string fixtures = Path.Combine(data, "animation-fixtures"); Directory.CreateDirectory(fixtures);
        using (var fixture = new Bitmap(4, 3, PixelFormat.Format32bppArgb)) { fixture.SetPixel(1, 1, Color.FromArgb(128, 255, 0, 0)); fixture.Save(Path.Combine(fixtures, "tiny.png"), ImageFormat.Png); }
        string valid = """
            {"layout":"horizontal_single_row","frameWidth":2,"frameHeight":3,"animations":{"idle":{"file":"missing-folder/tiny.png","frames":2,"fps":8,"loop":true,"origin":"bottom-center","alpha":true}}}
            """;
        string manifest = Path.Combine(fixtures, "animations.json");
        File.WriteAllText(manifest, valid);
        Require(new AnimationCatalog(fixtures).Get("idle").Error == null, "Unique fallback failed");
        foreach (string broken in new[] { valid.Replace("\"fps\":8", "\"fps\":0"), valid.Replace("\"frames\":2", "\"frames\":3"), valid.Replace("missing-folder/tiny.png", "absent.png"), valid.Replace("missing-folder/tiny.png", "../tiny.png") })
        {
            File.WriteAllText(manifest, broken); Require(new AnimationCatalog(fixtures).Get("idle").Error != null, "Invalid manifest entry accepted");
        }
        File.WriteAllText(manifest, valid); Directory.CreateDirectory(Path.Combine(fixtures, "second"));
        File.Copy(Path.Combine(fixtures, "tiny.png"), Path.Combine(fixtures, "second", "tiny.png"));
        Require(new AnimationCatalog(fixtures).Get("idle").Error!.Contains("ambigu", StringComparison.Ordinal), "Ambiguous basename fallback accepted");
        File.WriteAllText(manifest, valid.Replace("missing-folder/tiny.png", "tiny.png")); File.WriteAllText(Path.Combine(fixtures, "tiny.png"), "invalid PNG");
        Require(new AnimationCatalog(fixtures).Get("idle").Error != null, "Corrupt PNG accepted");
        File.Delete(manifest);
        bool missing = false; try { _ = new AnimationCatalog(fixtures); } catch (FileNotFoundException) { missing = true; }
        Require(missing, "Missing manifest silently guessed frame counts");
        results.Add("missing, corrupt, ambiguous, outside-root or inconsistent sprite data produces explicit errors");

        using (var player = new AnimationPlayer(catalog, 128))
        {
            Task<bool> obsolete = player.PlayAsync("dangle"); Task<bool> latest = player.PlayAsync("idle");
            Require(await latest && !await obsolete && player.Current?.Name == "idle", "Stale asynchronous load won");
            player.Stop(); int frozen = player.FrameIndex; await Task.Delay(150); Require(player.FrameIndex == frozen && !player.Playing, "Stop did not freeze player");
            Require(await player.PlayAsync("blink"), "Blink unavailable");
            await UntilAsync(() => !player.Playing); Require(player.FrameIndex == 3, "One-shot did not hold final frame");
            Task<bool> cancelled = player.PlayAsync("dance"); player.Stop(); Require(!await cancelled, "Cancelled decoder installed frames");
            string? playbackError = null; player.Error += text => playbackError = text;
            Require(!await player.PlayAsync("absent") && playbackError?.Contains("non déclarée", StringComparison.Ordinal) == true && !player.Playing, "Invalid runtime animation failed silently");
        }
        results.Add("animation loads cancel safely, latest selection wins, stop freezes and single clips complete");

        string? applied = null;
        using (var preview = new AnimationPreviewWindow(catalog, name => applied = name, () => { }) { Opacity = 0, ShowInTaskbar = false })
        {
            preview.Show(owner);
            Find<Button>(preview, "PlayAnimation").PerformClick();
            await UntilAsync(() => preview.Player.Playing && preview.Player.Current?.Name == "idle");
            await UntilAsync(() => Find<Label>(preview, "AnimationFrequency").Text.Contains("/s", StringComparison.Ordinal));
            Render(preview, Path.Combine(output, "animations.png"));
            Find<Button>(preview, "StopAnimation").PerformClick(); Require(!preview.Player.Playing, "Preview stop failed");
            Find<ComboBox>(preview, "Animation").SelectedItem = catalog.Get("mouth");
            Require(!Find<Button>(preview, "ApplyAnimation").Enabled && Find<Button>(preview, "PlayAnimation").Enabled, "Mouth overlay can replace/composite the desktop body");
            Find<Button>(preview, "PlayAnimation").PerformClick(); await UntilAsync(() => preview.Player.Current?.Name == "mouth");
            Require(preview.Player.RetainedBytes == 5L * 256 * 384 * 4, "Mouth preview retained unrelated body animation");
            Find<ComboBox>(preview, "Animation").SelectedItem = catalog.Get("walk"); Find<Button>(preview, "ApplyAnimation").PerformClick();
            Require(applied == "walk", "Preview desktop action failed"); preview.Close();
        }
        results.Add("model-free preview selects, plays, stops and reports frequency; mouth remains an isolated layer");

        var negativeWorkArea = new Rectangle(-1920, -200, 1920, 1080);
        Require(CharacterWindow.ClampAnchor(new(-5000, 9999), new(171, 256), negativeWorkArea) == new Point(-1835, 880), "Negative monitor clamping failed");
        Require(CharacterWindow.ClampAnchor(new(99999, -99999), new(171, 256), negativeWorkArea) == new Point(-86, 56), "Off-screen position clamping failed");
        results.Add("bottom-center anchoring clamps off-screen positions on negative-coordinate monitors");

        string? error = null; Point saved = Point.Empty;
        using (var character = new CharacterWindow(catalog, 50, false, null, p => saved = p, e => error = e, () => { }, nativeRendering: false) { Opacity = 0 })
        {
            Task pendingAppearance = character.ShowCharacterAsync(owner);
            await character.ApplyAppearanceAsync(50, false, false, owner); await pendingAppearance;
            Require(!character.Visible && !character.Player.Playing, "Hidden character completed a stale appearance load");
            Task appearance = character.ShowCharacterAsync(owner);
            await character.SetThinkingAsync(true); await character.SetSpeakingAsync(true); await character.ReactAsync("happy");
            await appearance;
            Require(character.Visible, "Early speech interrupted appearance before the character was shown");
            await character.SetSpeakingAsync(false); await character.SetThinkingAsync(false);
            await UntilAsync(() => character.Player.Current?.Name == "idle");
            Point anchor = character.BottomAnchor;
            await character.SetThinkingAsync(true); Require(character.Player.Current?.Name == "thinking", "Thinking not connected");
            await character.SetSpeakingAsync(true); Require(character.Player.Current?.Name == "talk", "Audio did not select full-body talk");
            await character.SetSpeakingAsync(false); Require(character.Player.Current?.Name == "thinking", "Thinking was not restored after speech");
            await character.SetThinkingAsync(false);
            await character.ApplyAppearanceAsync(100, false, true, owner); Require(character.BottomAnchor == anchor && character.Height == 256, "Scaling moved the foot anchor");
            await character.EvaluateInactivityAsync(121); Require(character.Player.Current?.Name == "sleep", "Local idle did not sleep");
            await character.NotifyActivityAsync(); Require(character.Player.Current?.Name == "wake", "Local activity did not wake");
            await UntilAsync(() => character.Player.Current?.Name == "idle");
            await character.WalkAsync(-100); Require(character.Player.Current?.Name == "walk", "Explicit walk absent");
            await UntilAsync(() => character.Player.Current?.Name == "idle"); Require(saved != Point.Empty && character.BottomAnchor.X < anchor.X, "Walk did not move/save own form");
            await character.ReactAsync("mouth"); Require(error?.Contains("sans surcouche", StringComparison.Ordinal) == true && character.Player.Current?.Name != "mouth", "Mouth full-body safety failed");
            await character.ReactAsync("leave"); await UntilAsync(() => !character.Visible);
            await character.ShowCharacterAsync(owner); await UntilAsync(() => character.Player.Current?.Name == "idle");
            character.Stop(); Task blocked = character.ReactAsync("dance"); await blocked;
            Require(character.IsStopped && !character.Player.Playing && character.Player.Current?.Name == "idle", "Emergency revived character animation");
            character.Close();
        }
        results.Add("character lifecycle connects arrive, idle, thinking, audio talk, sleep/wake, explicit walk and leave; stop is final");

        // Exercise the real native alpha presenter on a window owned by this test,
        // without reading any desktop pixels or showing a foreign window.
        using (var native = new CharacterWindow(catalog, 50, false, null, _ => { }, _ => { }, () => { }))
        using (SpriteFrames clip = SpriteFrames.Load(catalog.Get("idle"), 128, default))
            LayeredSprite.Present(native.Handle, clip.Frames[0], Point.Empty);
        results.Add("native per-pixel-alpha presenter accepts a test-owned layered window and releases its GDI resources");

        using (var emergency = new EmergencyStopService())
        using (var root = new MainWindow(new LocalSettingsStore(Path.Combine(data, "animation-menu")), new IrisSettings { CharacterVisible = false }, emergency,
            new NoHotkeys(), animations: catalog) { Opacity = 0, ShowInTaskbar = false })
        {
            root.Show(owner);
            Require(!Find<Button>(root, "Conversation").Enabled && Find<Button>(root, "Animations").Enabled, "Preview depends on inference");
            Find<Button>(root, "Animations").PerformClick();
            var preview = Application.OpenForms.OfType<AnimationPreviewWindow>().Single(); preview.Opacity = 0;
            Find<Button>(preview, "PlayAnimation").PerformClick(); await UntilAsync(() => preview.Player.Playing);
            root.Stop(); Require(preview.IsDisposed && !Find<Button>(root, "Animations").Enabled, "Emergency did not close preview");
            root.Close();
        }
        results.Add("main animation menu works without inference and emergency stop closes the preview");
        return results;
    }
    private sealed class NoHotkeys : IHotkeyRegistrar
    {
        public bool Register(IntPtr window, int functionKey) => true;
        public void Unregister(IntPtr window) { }
    }
}
