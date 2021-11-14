using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    const int playerAccountNameAndPassword = 1;

    string playerAccountDataPath;

    int playerWaitingForMatchWithID = -1;

    LinkedList<GameRoom> gameRooms;

    // Start is called before the first frame update
    void Start()
    { 
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccountDataPath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt";

        playerAccounts = new LinkedList<PlayerAccount>();

        LoadPlayerAccount();

        gameRooms = new LinkedList<GameRoom>();


        //Test for loading players
        /*
        foreach (PlayerAccount pa in playerAccounts)
        {
            Debug.Log(pa.name + "," + pa.password);
        }
        */
        {

        }
    }

    // Update is called once per frame
    void Update()
    {
        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }
    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.CreateAccount)
        {
            Debug.Log("Create Account");

            string n = csv[1];
            string p = csv[2];
            bool nameInUse = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameInUse = true;
                    break;
                }
            }

            if (nameInUse)
            {
                SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "", id);
            }
            else
            {
                PlayerAccount newPlayerAccount = new PlayerAccount(n, p);
                playerAccounts.AddLast(newPlayerAccount);
                SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "", id);

                SavePlayerAccount();
            }

            //If not, create new account, add to list and save to list
            //Send Client success or failure
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {
            Debug.Log("Login to Account");

            string n = csv[1];
            string p = csv[2];

            bool hasNameBeenFound = false;
            bool hasMsgBeenSentToClient = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    hasNameBeenFound = true;

                    if (pa.password == p)
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                        hasMsgBeenSentToClient = true;
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                        hasMsgBeenSentToClient = true;
                    }
                }
                else
                {
                    //?
                }
            }

            if (!hasNameBeenFound)
            {
                if (!hasMsgBeenSentToClient)
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                }
            }

            //Check if player account already exists
            //Send client success/failure

        }
        else if (signifier == ClientToServerSignifiers.JoinQueueForGameRoom)
        {
            if (playerWaitingForMatchWithID == -1)
            {
                playerWaitingForMatchWithID = id;
            }
            else
            {
                GameRoom gr = new GameRoom(playerWaitingForMatchWithID, id);

                gameRooms.AddLast(gr);

                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID1);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID2);

                playerWaitingForMatchWithID = -1;
            }
        }
        else if (signifier == ClientToServerSignifiers.PlayGame)
        {
            GameRoom gr = GetGameRoomWithClientID(id);

            if (gr != null)
            {
                if (gr.playerID1 == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlayed + "", gr.playerID2);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlayed + "", gr.playerID1);
                }
            }
        }
    }

    private void SavePlayerAccount()
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt");

        foreach (PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(playerAccountNameAndPassword + "," + pa.name + "," + pa.password);
        }

        sw.Close();
    }

    private void LoadPlayerAccount()
    {

        if (File.Exists(playerAccountDataPath))
        {
            StreamReader sr = new StreamReader(playerAccountDataPath);

            string line;

            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);

                if (signifier == playerAccountNameAndPassword)
                {
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                    playerAccounts.AddLast(pa);
                }
            }

            sr.Close();
        }
    }


    private GameRoom GetGameRoomWithClientID(int id)
    {
        foreach(GameRoom gr in gameRooms)
        {
            if (gr.playerID1 == id || gr.playerID2 == id)
            {
                return gr;
            }
            return null;
        }
    }



  
}




public class PlayerAccount
{
    public string name;
    public string password;

    public PlayerAccount(string Name, string Password)
    {
        name = Name;
        password = Password;
    }
}


public class GameRoom
{
    public int playerID1;
    public int playerID2;

    public GameRoom(int PlayerID1, int PlayerID2)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;
    }
}


public static class ClientToServerSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;

    public const int JoinQueueForGameRoom = 3;

    public const int PlayGame = 4;
}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 1;

    public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;

    public const int AccountCreationFailed = 4;

    public const int OpponentPlayed = 5;

    public const int GameStart = 6;
}

