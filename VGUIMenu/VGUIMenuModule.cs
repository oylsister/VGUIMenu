using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using VGUIMenu.Rendering;
using VGUIMenu.Shared;

namespace VGUIMenu;

public sealed class VGUIMenuModule : IModSharpModule, IClientListener, IGameListener
{
    public string DisplayName   => "VGUIMenu";
    public string DisplayAuthor => "Oylsister";

    /// <summary>How often timeouts are checked and lost HUDs are restored.</summary>
    private const double ThinkInterval = 0.25;

    private readonly ILogger<VGUIMenuModule> _logger;
    private readonly IModSharp               _modSharp;
    private readonly ISharpModuleManager     _modules;
    private readonly IClientManager          _clients;
    private readonly IConVarManager          _conVars;
    private readonly string                  _sharpPath;
    private readonly MenuSystem              _system;

    private readonly IClientManager.DelegateClientCommand[] _keyCommands = new IClientManager.DelegateClientCommand[Menu.MaxPageItems];

    private MenuConfig    _config;
    private TestMenus?    _testMenus;
    private Guid?         _thinkTimer;
    private IConVar?      _soundsConVar;

    private const string SoundsConVarName = "ms_vguimenu_sounds";

    public VGUIMenuModule(ISharedSystem sharedSystem,
        string                          dllPath,
        string                          sharpPath,
        Version                         version,
        IConfiguration                  coreConfiguration,
        bool                            hotReload)
    {
        _logger    = sharedSystem.GetLoggerFactory().CreateLogger<VGUIMenuModule>();
        _modSharp  = sharedSystem.GetModSharp();
        _modules   = sharedSystem.GetSharpModuleManager();
        _clients   = sharedSystem.GetClientManager();
        _conVars   = sharedSystem.GetConVarManager();
        _sharpPath = sharpPath;

        MenuCallbacks.Logger = _logger;

        _config = MenuConfig.Load(sharpPath, _logger);
        _system = new MenuSystem(sharedSystem.GetPanoramaManager(),
                                 sharedSystem.GetTransmitManager(),
                                 () => _config,
                                 _modSharp.EngineTime,
                                 _logger,
                                 () => _soundsConVar?.GetBool() ?? true);
    }

#region IModSharpModule

    public bool Init()
    {
        _clients.InstallClientListener(this);
        _modSharp.InstallGameListener(this);

        // On hot reload the ConVar already exists, so reuse it.
        _soundsConVar = _conVars.CreateConVar(SoundsConVarName, true, "Play VGUIMenu select / exit sounds (also needs Sounds.Enabled in vguimenu.jsonc).")
                        ?? _conVars.FindConVar(SoundsConVarName, false);

        for (var i = 0; i < _keyCommands.Length; i++)
        {
            var key = i + 1;

            // "1" registers ms_1 plus the chat triggers (!1, and silent /1).
            _keyCommands[i] = (client, _) => _system.ClientPressedKey(client, key) ? ECommandAction.Stopped : ECommandAction.Skipped;
            _clients.InstallCommandCallback(key.ToString(), _keyCommands[i]);
        }

        if (_config.EnableTestCommand)
        {
            _testMenus = new TestMenus(_system);
            _clients.InstallCommandCallback(TestMenus.Command, _testMenus.OnCommand);
        }

        _thinkTimer = _modSharp.PushTimer(_system.Think, ThinkInterval, GameTimerFlags.Repeatable);

        return true;
    }

    public void PostInit()
    {
        _modules.RegisterSharpModuleInterface<IVGUIMenuManager>(this, IVGUIMenuManager.Identity, _system);
    }

    public void OnAllModulesLoaded()
        => _system.ExternalMenus = StockMenuSource.TryCreate(_modules, _logger);

    /// <summary>MenuManager loaded after us (or reloaded): pick it up.</summary>
    public void OnLibraryConnected(string name)
    {
        if (_system.ExternalMenus is null)
        {
            _system.ExternalMenus = StockMenuSource.TryCreate(_modules, _logger);
        }
    }

    public void Shutdown()
    {
        if (_thinkTimer is { } timer)
        {
            _modSharp.StopTimer(timer);
            _thinkTimer = null;
        }

        for (var i = 0; i < _keyCommands.Length; i++)
        {
            _clients.RemoveCommandCallback((i + 1).ToString(), _keyCommands[i]);
        }

        if (_testMenus is not null)
        {
            _clients.RemoveCommandCallback(TestMenus.Command, _testMenus.OnCommand);
        }

        _system.Shutdown();
        _system.ExternalMenus = null;
        _soundsConVar = null;

        _modSharp.RemoveGameListener(this);
        _clients.RemoveClientListener(this);
    }

#endregion

    /// <summary>vguimenu.jsonc is re-read on every map start.</summary>
    private void ReloadConfig()
        => _config = MenuConfig.Load(_sharpPath, _logger);

#region IClientListener

    int IClientListener.ListenerVersion  => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;

    public void OnClientPutInServer(IGameClient client)
        => _system.OnClientPutInServer(client);

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
        => _system.OnClientDisconnected(client);

#endregion

#region IGameListener

    int IGameListener.ListenerVersion  => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 0;

    public void OnResourcePrecache()
        => _modSharp.PrecacheResource(_config.LayoutPath);

    public void OnGameActivate()
        => ReloadConfig();

    public void OnGameDeactivate()
        => _system.OnMapEnd();

#endregion
}
