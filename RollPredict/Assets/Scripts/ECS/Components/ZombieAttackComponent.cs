// using System;
// using Frame.FixMath;
//
// namespace Frame.ECS
// {
//     /// <summary>
//     /// 僵尸攻击组件：存储攻击相关配置和状态
//     /// </summary>
//     [Serializable]
//     public struct ZombieAttackComponent : IComponent
//     {
//         /// <summary>
//         /// 攻击范围（外层触发器半径，玩家进入此范围时触发攻击）
//         /// </summary>
//         public Fix64 attackRange;
//         
//         /// <summary>
//         /// 攻击伤害
//         /// </summary>
//         public int damage;
//         
//         /// <summary>
//         /// 攻击前摇时间（帧数）
//         /// </summary>
//         public int windupFrames;
//         
//         /// <summary>
//         /// 攻击后摇时间（帧数）
//         /// </summary>
//         public int cooldownFrames;
//         
//         /// <summary>
//         /// 攻击伤害判定距离（前方扇形/矩形的距离）
//         /// </summary>
//         public Fix64 attackDamageRange;
//         
//         /// <summary>
//         /// 攻击伤害判定角度（扇形角度，弧度制）
//         /// 例如：60度 = 60 * PI / 180
//         /// </summary>
//         public Fix64 attackDamageAngle;
//         
//         public ZombieAttackComponent(
//             Fix64 attackRange,
//             int damage,
//             int windupFrames,
//             int cooldownFrames,
//             Fix64 attackDamageRange,
//             Fix64 attackDamageAngle)
//         {
//             this.attackRange = attackRange;
//             this.damage = damage;
//             this.windupFrames = windupFrames;
//             this.cooldownFrames = cooldownFrames;
//             this.attackDamageRange = attackDamageRange;
//             this.attackDamageAngle = attackDamageAngle;
//         }
//         
//         public object Clone()
//         {
//             return this;
//         }
//         
//         public override string ToString()
//         {
//             return $"{GetType().Name}: range={attackRange}, damage={damage}, windup={windupFrames}, cooldown={cooldownFrames}";
//         }
//     }
// }
//
