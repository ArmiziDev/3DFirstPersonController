using Godot;
using System;
using System.Linq;

public partial class MainMenu : Control
{
	[Export] public string worldPath = "res://scenes/world.tscn";
    [Export] private int port = 8910;
	private string address = "127.0.0.1";
	private string name_of_player;
	private ENetMultiplayerPeer peer;
	private ENetConnection.CompressionMode compressionMode = ENetConnection.CompressionMode.RangeCoder;

	//Menu
	private Control loginMenu;
	private Control startMenu;
	private Control lobbyMenu;
	private Control messageBox;

	private bool inServer = false;

    public override void _Ready()
	{
		InitializeMenu();
		InitializeMultiplayerAttributes();
	}

	private void InitializeMenu()
	{
		loginMenu = GetNode<Control>("%LoginMenu");
		startMenu = GetNode<Control>("%StartMenu");
		lobbyMenu = GetNode<Control>("%LobbyMenu");
		messageBox = GetNode<Control>("%MessageBox");

		loginMenu.Visible = true;
		startMenu.Visible = false;
		lobbyMenu.Visible = false;
		messageBox.Visible = false;

		GetNode<Button>("%StartGame").Hide();
	}

    private void InitializeMultiplayerAttributes()
    {
        Multiplayer.PeerConnected += PeerConnected;
		Multiplayer.PeerDisconnected += PeerDisconnected;
		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ConnectionFailed += ConnectionFailed;
    }

	// runs when connection fails and only on the client
    private void ConnectionFailed()
    {
        PrintError("CONNECTION FAILED");
    }

	// runs when connections success and only on the client
    private void ConnectedToServer()
    {
        PrintMessage("CONNECTED TO SERVER");
		// Sends specifically only to server (host)
		RpcId(1, nameof(sendPlayerInformation), Multiplayer.GetUniqueId(), name_of_player);
		PrintMessage("Connected With Username: " + name_of_player);
    }

	// runs when a player disconnects and runs on all peers
    private void PeerDisconnected(long id)
    {
		PlayerInfo player = Globals.PLAYERS.Find(p => p.server_id == id);
        PrintError(player.Name.ToString() + " Disconnected");
    }

	// runs when a player connects and runs on all peers
    private void PeerConnected(long id)
	{
		PrintMessage("Player Connected");
	}

	public void _on_host_button_down()
	{
		peer = new ENetMultiplayerPeer();
		var error = peer.CreateServer(port, 10); // Supports 10 Clients
		if (error != Error.Ok)
		{
			GD.PrintErr("error cannot host! : " + error.ToString());
			return;
		}
		GetNode<Button>("%StartGame").Show();
		GetNode<Button>("%Join").Hide();
		StartLobbyMenu();

		peer.Host.Compress(compressionMode);

		Multiplayer.MultiplayerPeer = peer; // making urself host
		PrintMessage("WAITING FOR PLAYERS!");

		sendPlayerInformation(1, name_of_player); // sending player information as host because host is player
		UpdateLobbyMenu();
	}

	private void StartLobbyMenu()
	{
		lobbyMenu.Show();
		messageBox.Show();
	}

	private void PrintMessage(String message)
	{
		GD.Print(message);
		Label tempMessage = new Label();
        tempMessage.Text = message;
        GetNode<VBoxContainer>("%MessageBoxContainer").AddChild(tempMessage);
	}

	private void PrintError(String message)
	{
		GD.PrintErr(message);
		Label tempMessage = new Label();
        tempMessage.Text = message;
        tempMessage.Modulate = new Color(1, 0, 0);  // Set the text color to red
        GetNode<VBoxContainer>("%MessageBoxContainer").AddChild(tempMessage);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void SendMessage(String message, String username)
	{
		Label tempMessage = new Label();
        tempMessage.Text = username + ": " + message;
        GetNode<VBoxContainer>("%MessageBoxContainer").AddChild(tempMessage);
	}

	private void message_text_submit()
	{
		LineEdit messageInputBox = GetNode<LineEdit>("%TypeMessage");
		if (messageInputBox.Text.Length > 0)
		{
			Rpc(nameof(SendMessage), messageInputBox.Text, Globals.localPlayerInfo.Name);
		}
		PrintMessage(Globals.localPlayerInfo.Name + ": " + messageInputBox.Text);
		messageInputBox.Text = "";
	}

	private void _on_send_message_pressed()
	{
		message_text_submit();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void UpdateLobbyMenu()
	{
		// Clear Lobby
		for(int i = 1; i <= 10; i++)
		{
			GetNode<Label>("%Player" + i.ToString()).Text = "";
		}

		// Initialize Lobby
		int currentRedPlayer = 1;
		int currentBluePlayer = 6;
		foreach(var player in Globals.PLAYERS)
		{
			if(player.player_team == Team.Red)
			{
				GetNode<Label>("%Player" + currentRedPlayer.ToString()).Text = player.Name;
				currentRedPlayer++;
			}
			else if(player.player_team == Team.Blue)
			{
				GetNode<Label>("%Player" + currentBluePlayer.ToString()).Text = player.Name;
				currentBluePlayer++;
			}
		}	
	}

	private void _on_join_button_down()
	{
		if (!inServer)
		{
			peer = new ENetMultiplayerPeer();
			peer.CreateClient(address, port);

			peer.Host.Compress(compressionMode);
			Multiplayer.MultiplayerPeer = peer;
			PrintMessage("Joining Game");
			inServer = true;
			StartLobbyMenu();
			GetNode<Button>("%Join").Text = "Disconnect";
			GetNode<Button>("%Host").Hide();
		}
	}

	private void _on_start_game_button_down()
	{
		Rpc(nameof(startGame));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void startGame()
	{
		GetNode<Button>("%StartGame").Hide();
		GetNode<LoadingScreen>("%LoadingScreen").LoadLevel(worldPath);
	}

	private void _on_join_team_red_button_down()
	{
		if (Globals.localPlayerInfo.player_team == Team.Red) return;
		// Call the RPC to update the player info on the server
		if(Multiplayer.IsServer())
		{
			updatePlayerInformation(Globals.localPlayerInfo.server_id, Globals.localPlayerInfo.Name, (int)Team.Red, Globals.localPlayerInfo.health);
		}
		else
		{
			Rpc(nameof(updatePlayerInformation), Globals.localPlayerInfo.server_id, Globals.localPlayerInfo.Name, (int)Team.Red, Globals.localPlayerInfo.health);
		}
	}

	private void _on_join_team_blue_button_down()
	{
		if (Globals.localPlayerInfo.player_team == Team.Blue) return;
		// Call the RPC to update the player info on the server
		if(Multiplayer.IsServer())
		{
			updatePlayerInformation(Globals.localPlayerInfo.server_id, Globals.localPlayerInfo.Name, (int)Team.Blue, Globals.localPlayerInfo.health);
		}
		else
		{
			Rpc(nameof(updatePlayerInformation), Globals.localPlayerInfo.server_id, Globals.localPlayerInfo.Name, (int)Team.Blue, Globals.localPlayerInfo.health);
		}
	}

	// Add the new RPC method for updating player information
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Needs to be called locally so server updates its own player
	private void updatePlayerInformation(int id, string name, int newTeam, int health)
	{
		//PrintMessage("Updating Player Information");
		// Check if the player exists in the global player list
		var player = Globals.PLAYERS.Find(p => p.server_id == id);
		
		if (player != null)
		{
			// Update the player's information
			player.player_team = (Team)newTeam;
			player.Name = name;
			player.health = health;
			
			// If the player being updated is the local player, update localPlayerInfo as well
			if (player.server_id == Multiplayer.GetUniqueId())
			{
				Globals.localPlayerInfo = player;
			}

			// If we're the server, broadcast this update to all other clients
			if (Multiplayer.IsServer())
			{
				Rpc(nameof(updatePlayerInformation), player.server_id, player.Name, (int)player.player_team, health);
			}
			
			// Update the lobby menu to reflect the team change
			Rpc(nameof(UpdateLobbyMenu));
		}
		else
		{
			PrintError("Player is Null");
		}
	}

	

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void sendPlayerInformation(int id, string name)
	{
		// Check if the player already exists in the list based on Name or server_id
		if (!Globals.PLAYERS.Any<PlayerInfo>(player => player.server_id == id))
		{
			//PrintMessage("Adding Player");
			PlayerInfo new_player = new PlayerInfo()
			{
				Name = name,
				server_id = id,
				health = 100,
				player_team = Team.Red
			};
			Globals.PLAYERS.Add(new_player);

			// If the added player is the same as your id, then we want to set that object in the server list to ur local player
			if (new_player.server_id == Multiplayer.GetUniqueId())
			{
				Globals.localPlayerInfo = new_player;
			}
		}
		else
		{
			// If it is alreay in list, we need to just update the information
		}

		if (Multiplayer.IsServer())
		{
			foreach (var player in Globals.PLAYERS)
			{
				Rpc(nameof(sendPlayerInformation), player.server_id, player.Name);
			}
		}
		Rpc(nameof(UpdateLobbyMenu));
	}


	private void _on_enter_button_down()
	{
		String input_name = GetNode<LineEdit>("%LineEdit").Text;
		if (input_name.Length < 5 || input_name.Length > 15)
		{
			GetNode<Label>("%ErrorMessage").Text = "Name Needs To Be Inbwteen 5 and 15 characters long";
			return;
		}
		else if (input_name.Contains(" "))
		{
			GetNode<Label>("%ErrorMessage").Text = "Spaces are not allowed in Name";
			return;
		}
		name_of_player = input_name;

		loginMenu.Visible = false;
		startMenu.Visible = true;
	}
}