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


    /// <summary>
    /// 从Entity保存状态到GameState
    /// Entity -> State
    /// </summary>
    public static void SaveToGameState(GameState gameState)
    {
        gameState.players.Clear();
        foreach (var (id, playerController) in players)
        {
            // state = entity
            gameState.players[id] = new PlayerState(id, playerController.HP);
        }
    }


    /// <summary>
    /// 从GameState恢复状态到Entity
    /// State -> Entity
    /// </summary>
    public static void RestoreFromGameState(GameState gameState)
    {
        foreach (var (id, playerState) in gameState.players)
        {
            // entity = state 
            if (players.TryGetValue(id, out var playerController))
            {
                playerController.HP = playerState.HP;
            }
        }
    }


}