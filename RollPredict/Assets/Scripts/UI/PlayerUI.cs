using UnityEngine;
using UnityEngine.UI;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家UI组件：挂载在玩家GameObject上，用于显示玩家相关的UI信息
    /// 
    /// 使用方式：
    /// 1. 在玩家Prefab上挂载此组件
    /// 2. 在Inspector中设置需要显示的UI元素（Text、Slider、Image等）
    /// 3. ECSSyncHelper会自动同步血量等信息到此组件
    /// 
    /// 设计原则：
    /// - 视图层代码，不包含游戏逻辑
    /// - 只负责显示，数据来源是ECSSyncHelper
    /// - 支持多种UI显示方式（文本、血条、图片等）
    /// </summary>
    public class PlayerUI : MonoBehaviour
    {
        [Header("血量显示")] [Tooltip("血量文本（可选）")] public Text healthText;


        [Tooltip("血量条填充图片（可选）")] public Image healthBarFill;

        [Header("其他UI（可扩展）")] [Tooltip("玩家名字文本（可选）")]
        public Text playerNameText;


        /// <summary>
        /// 更新血量显示
        /// </summary>
        /// <param name="currentHp">当前血量</param>
        /// <param name="maxHp">最大血量</param>
        public void UpdateHealth(int currentHp, int maxHp)
        {
            // 更新文本
            if (healthText != null)
            {
                healthText.text = $"{currentHp}/{maxHp}";

                // 根据血量百分比改变颜色
                float healthPercent = (float)currentHp / maxHp;
                if (healthPercent > 0.6f)
                    healthText.color = Color.green;
                else if (healthPercent > 0.3f)
                    healthText.color = Color.yellow;
                else
                    healthText.color = Color.red;
            }


            // 更新填充图片
            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = (float)currentHp / maxHp;

                // 根据血量百分比改变颜色
                float healthPercent = (float)currentHp / maxHp;
                healthBarFill.color = Color.Lerp(Color.red,Color.green,  healthPercent);
            }
        }

        /// <summary>
        /// 更新玩家名字显示
        /// </summary>
        public void UpdatePlayerName(string playerName)
        {
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }
        }
    }
}