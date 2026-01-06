using System.Collections.Generic;
using Frame.Core;

public static class PlayerHelper
{
    //id->entity
   public static Dictionary<int,PlayerController> players = new Dictionary<int,PlayerController>();
   

    public static void Register(PlayerController body)
    {
       
        players[body.playerId] = body;
        
    }


    public static void Unregister(PlayerController body)
    {
        players.Remove(body.playerId);
    }


    public static void SaveToGameState(GameState gameState)
    {
        gameState.players.Clear();
        foreach (var (id,playerController) in  players)
        {
            // state = entity
            gameState.players[id].playerId = id;
            gameState.players[id].HP = playerController.HP;
            
        }
    }


    public static void RestoreFromGameState(GameState gameState)
    {
        foreach (var (id,playerState) in  gameState.players)
        {
            // entity = state 
            if (players.TryGetValue(id, out var playerController))
            {
                playerController.HP = playerState.HP;
            }
        }
    }


}