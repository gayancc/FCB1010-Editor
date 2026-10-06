using System.Text.Json;
using FCB1010.App.ViewModels;
using FCB1010.Core;
using FCB1010.Midi;
using Melanchall.DryWetMidi.Core;

namespace FCB1010.Tests;

public sealed class ProductionRegressionTests
{
    private static byte[] StockFixture() => Convert.FromHexString(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "stock", "fcbtool-vamp.hex")).Trim());

    [Fact]
    public void Footswitch_selection_refreshes_all_preset_bound_editor_fields()
    {
        using var viewModel = new MainViewModel();
        viewModel.Config.PresetNames[1] = "Second preset";
        viewModel.Config.Presets[1].ExpressionA.Controller = 74;
        viewModel.Config.Presets[1].Switch1Closed = true;

        viewModel.SelectFootswitchCommand.Execute(2);

        Assert.Equal(2, viewModel.Footswitch);
        Assert.Equal("Second preset", viewModel.PresetName);
        Assert.Equal(74, viewModel.ExpressionAController);
        Assert.True(viewModel.Switch1Closed);
    }

    [Fact]
    public async Task Project_loader_rejects_duplicate_preset_indexes_before_they_reach_the_editor()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fcb1010-invalid-{Guid.NewGuid():N}.fcbproject");
        try
        {
            var config = FcbSysExCodec.Parse(StockFixture(), FirmwareFamily.Stock);
            config.Presets[1].Index = 0;
            var payload = new FcbProject { Configuration = config };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => ProjectPersistence.LoadProjectAsync(path));
            Assert.Contains("duplicated", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Project_loader_rejects_invalid_embedded_sysex()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fcb1010-invalid-raw-{Guid.NewGuid():N}.fcbproject");
        try
        {
            var payload = new FcbProject
            {
                Configuration = FcbSysExCodec.Parse(StockFixture(), FirmwareFamily.Stock),
                RawSysExBase64 = Convert.ToBase64String([0xF0, 0x01, 0xF7]),
            };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));

            await Assert.ThrowsAsync<SysExFormatException>(() => ProjectPersistence.LoadProjectAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Project_and_sysex_saves_replace_files_without_leaving_temporary_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fcb1010-atomic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var config = FcbSysExCodec.Parse(StockFixture(), FirmwareFamily.Stock);
            var projectPath = Path.Combine(directory, "test.fcbproject");
            var sysexPath = Path.Combine(directory, "test.syx");
            await ProjectPersistence.SaveProjectAsync(projectPath, config);
            config.PresetNames[0] = "Replacement";
            await ProjectPersistence.SaveProjectAsync(projectPath, config);
            await ProjectPersistence.SaveSysExAsync(sysexPath, config);

            Assert.Equal("Replacement", (await ProjectPersistence.LoadProjectAsync(projectPath)).PresetNames[0]);
            Assert.Equal(FcbSysExCodec.DumpLength, new FileInfo(sysexPath).Length);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp-*"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Device_write_requires_backup_then_verifies_readback_byte_for_byte()
    {
        var backupDirectory = Path.Combine(Path.GetTempPath(), $"fcb1010-backup-{Guid.NewGuid():N}");
        try
        {
            var dump = StockFixture();
            var transport = new LoopbackTransport(dump);
            using var service = new FcbDeviceService(transport, backupDirectory) { PostWriteSettle = TimeSpan.Zero };
            var config = FcbSysExCodec.Parse(dump, FirmwareFamily.Stock);

            var withoutBackup = await service.WriteDumpAsync(config);
            Assert.False(withoutBackup.Completed);
            Assert.Contains("backup", withoutBackup.Message, StringComparison.OrdinalIgnoreCase);

            Assert.True((await service.ReceiveDumpAsync(TimeSpan.FromSeconds(1), FirmwareFamily.Stock)).Completed);
            config.Presets[0].ProgramChanges[0].Program = 42;
            var result = await service.WriteDumpAsync(config);

            Assert.True(result.Completed, result.Message);
            Assert.Single(Directory.EnumerateFiles(backupDirectory, "*.syx"));
            Assert.Equal(FcbSysExCodec.Serialize(config), transport.CurrentDump);
        }
        finally { if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, recursive: true); }
    }

    private sealed class LoopbackTransport(byte[] initialDump) : IMidiTransport
    {
        public byte[] CurrentDump { get; private set; } = initialDump.ToArray();
        public TransportState State => TransportState.Connected;
        public string? InputPortName => "Test IN";
        public string? OutputPortName => "Test OUT";
        public event EventHandler<TransportState>? StateChanged { add { } remove { } }
        public event EventHandler<MidiDiagnostic>? Diagnostic { add { } remove { } }
        public event EventHandler<byte[]>? SysExReceived;
        public IReadOnlyList<MidiPortInfo> GetInputPorts() => [new("0", "Test IN")];
        public IReadOnlyList<MidiPortInfo> GetOutputPorts() => [new("0", "Test OUT")];
        public Task ConnectAsync(string inputId, string outputId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendMidiEventAsync(MidiEvent midiEvent, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSysExAsync(byte[] message, CancellationToken ct = default)
        {
            if (message.AsSpan().SequenceEqual(FcbDeviceService.FullDumpRequest))
                SysExReceived?.Invoke(this, CurrentDump.ToArray());
            else if (message.Length == FcbSysExCodec.DumpLength)
                CurrentDump = message.ToArray();
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }
}
