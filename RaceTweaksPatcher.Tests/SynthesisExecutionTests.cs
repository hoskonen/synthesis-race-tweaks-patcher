using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace RaceTweaksPatcher.Tests;

[TestClass]
public sealed class SynthesisExecutionTests
{
    [TestMethod]
    [DataRow(true, false, FlagChange.Enable, 0)]
    [DataRow(true, true, FlagChange.Enable, 0)]
    [DataRow(false, false, FlagChange.Disable, 0)]
    [DataRow(false, false, FlagChange.Enable, 1)]
    public async Task RunPatcherCompletesAndExportsExpectedRecords(
        bool dryRun, bool hasSource, FlagChange change, int overridesWritten)
    {
        var scratch = Directory.CreateTempSubdirectory("race-tweaks-");
        try
        {
            var data = scratch.CreateSubdirectory("Data").FullName;
            var masterKey = ModKey.FromFileName("Skyrim.esm");
            var master = new SkyrimMod(masterKey, SkyrimRelease.SkyrimSE);
            var giant = master.Races.AddNew("GiantRace");
            var masterPath = Path.Combine(data, masterKey.FileName);
            master.WriteToBinary(masterPath);
            var masterBytes = File.ReadAllBytes(masterPath);

            var plugins = Path.Combine(scratch.FullName, "plugins.txt");
            File.WriteAllText(plugins, "*Skyrim.esm\n");
            File.WriteAllText(Path.Combine(scratch.FullName, "settings.json"), JsonSerializer.Serialize(new PatcherSettings
            {
                DryRun = dryRun,
                RaceTweaks = [new() { Race = giant.FormKey.ToString(), NoKnockdowns = change }]
            }, new JsonSerializerOptions { IncludeFields = true }));

            var patchKey = ModKey.FromFileName("Test.esp");
            var output = Path.Combine(scratch.CreateSubdirectory("Output").FullName, patchKey.FileName);
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = scratch.FullName
            };
            string[] args =
            [
                typeof(Program).Assembly.Location, "run-patcher", "--GameRelease", "SkyrimSE",
                "--DataFolderPath", data, "--LoadOrderFilePath", plugins, "--ExtraDataFolder", scratch.FullName,
                "--ModKey", patchKey.FileName, "--OutputPath", output, "--LoadOrderIncludesCreationClub", "true"
            ];
            foreach (var arg in args)
                start.ArgumentList.Add(arg);

            string? source = null;
            byte[]? sourceBytes = null;
            if (hasSource)
            {
                var preceding = new SkyrimMod(patchKey, SkyrimRelease.SkyrimSE);
                preceding.Races.GetOrAddAsOverride(giant).Flags = Race.Flag.Playable;
                source = Path.Combine(scratch.CreateSubdirectory("Source").FullName, patchKey.FileName);
                preceding.WriteToBinary(source);
                sourceBytes = File.ReadAllBytes(source);
                start.ArgumentList.Add("--SourcePath");
                start.ArgumentList.Add(source);
            }

            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }

            var log = await stdout;
            Assert.AreEqual(0, process.ExitCode, log + await stderr);
            StringAssert.Contains(log, $"Overrides written: {overridesWritten}");
            if (dryRun)
                StringAssert.Contains(log, "Status: WOULD MODIFY");
            Assert.IsTrue(File.Exists(output), log);

            using var result = SkyrimMod.CreateFromBinaryOverlay(new ModPath(patchKey, output), SkyrimRelease.SkyrimSE);
            Assert.AreEqual(hasSource ? 1 : overridesWritten, result.Races.Count);
            if (hasSource || overridesWritten > 0)
                Assert.AreEqual(hasSource ? Race.Flag.Playable : Race.Flag.NoKnockdowns, result.Races[giant.FormKey].Flags);

            CollectionAssert.AreEqual(masterBytes, File.ReadAllBytes(masterPath));
            if (source is not null)
                CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
