using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using TerrariaApi.Server;
using Terraria;
using Wolfje.Plugins.SEconomy.Configuration.WorldConfiguration;
using Wolfje.Plugins.SEconomy.Journal;
using TShockAPI;

namespace Wolfje.Plugins.SEconomy
{

    /// <summary>
    /// World economy. Provides monetary gain and loss as a 
    /// result of interaction in the world, including mobs 
    /// and players.
    /// </summary>
    public class WorldEconomy : IDisposable
    {
        protected SEconomy Parent { get; set; }

        /// <summary>
        /// Gets or sets a multiplier for mob kills and deaths
        /// after calculation.
        /// </summary>
        public int CustomMultiplier { get; set; }

        /// <summary>
        /// Format for this dictionary:
        /// Key: NPC
        /// Value: A list of players who have done damage to the NPC
        /// </summary>
        private Dictionary<NPC, List<PlayerDamage>> DamageDictionary = new Dictionary<NPC, List<PlayerDamage>>();

        /// <summary>
        /// Format for this dictionary:
        /// * key: Player ID
        /// * value: Last player hit ID
        /// </summary>
        protected Dictionary<int, int> PVPDamage = new Dictionary<int, int>();

        /// <summary>
        /// synch object for access to the dictionary.  You MUST obtain 
        /// a mutex through this object to access the dictionary member.
        /// </summary>
        protected readonly object __dictionaryMutex = new object();

        /// <summary>
        /// synch object for access to the pvp dictionary.  You MUST obtain
        /// a mutex through this object to access the dictionary member.
        /// </summary>
        protected readonly object __pvpDictMutex = new object();

        /// <summary>
        /// Synch object for NPC damage, forcing NPC damages to be serialized
        /// </summary>
        protected static readonly object __NPCDamageMutex = new object();

        /// <summary>
        /// World configuration node, from TShock\SEconomy\SEconomy.WorldConfig.json
        /// </summary>
        public Configuration.WorldConfiguration.WorldConfig WorldConfiguration { get; private set; }


        public WorldEconomy(SEconomy parent)
        {
            this.WorldConfiguration = Configuration.WorldConfiguration.WorldConfig.LoadConfigurationFromFile(
                "tshock" + System.IO.Path.DirectorySeparatorChar + "SEconomy" + System.IO.Path.DirectorySeparatorChar + "SEconomy.WorldConfig.json");
            this.Parent = parent;

            ServerApi.Hooks.NetGetData.Register(Parent.PluginInstance, NetHooks_GetData);
            ServerApi.Hooks.NetSendData.Register(Parent.PluginInstance, NetHooks_SendData);
            ServerApi.Hooks.GameUpdate.Register(Parent.PluginInstance, Game_Update);

            this.CustomMultiplier = 1;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(Parent.PluginInstance, NetHooks_GetData);
                ServerApi.Hooks.NetSendData.Deregister(Parent.PluginInstance, NetHooks_SendData);
                ServerApi.Hooks.GameUpdate.Deregister(Parent.PluginInstance, Game_Update);
            }
        }

        protected void Game_Update(EventArgs args)
        {
            foreach (NPC npc in Main.npc)
            {
                if (npc == null || npc.townNPC == true || npc.lifeMax == 0)
                {
                    continue;
                }

                if (npc.active == false)
                {
                    GiveRewardsForNPC(npc);
                }
            }
        }

        #region "NPC Reward handling"

        /// <summary>
        /// Adds damage done by a player to an NPC slot.  When the NPC dies the rewards for it will fill out.
        /// </summary>
        protected void AddNPCDamage(NPC NPC, Player Player, int Damage, bool crit = false)
        {
            List<PlayerDamage> damageList = null;
            PlayerDamage playerDamage = null;
            double dmg;


            if (Player == null || NPC.active == false || NPC.life <= 0)
            {
                return;
            }

            lock (__dictionaryMutex)
            {
                if (DamageDictionary.ContainsKey(NPC))
                {
                    damageList = DamageDictionary[NPC];
                }
                else {
                    damageList = new List<PlayerDamage>(1);
                    DamageDictionary.Add(NPC, damageList);
                }
            }

            lock (__NPCDamageMutex)
            {
                if ((playerDamage = damageList.FirstOrDefault(i => i.Player == Player)) == null)
                {
                    playerDamage = new PlayerDamage() { Player = Player };
                    damageList.Add(playerDamage);
                }

                if ((dmg = (crit ? 2 : 1) * Main.CalculateDamageNPCsTake(Damage, NPC.ichor ? NPC.defense - 20 : NPC.defense)) > NPC.life)
                {
                    dmg = NPC.life;
                }
            }
            playerDamage.Damage += dmg;

            if (playerDamage.Damage > NPC.lifeMax)
            {
                playerDamage.Damage -= playerDamage.Damage % NPC.lifeMax;
            }
        }

        /// <summary>
        /// Should occur when an NPC dies; gives rewards out to all the players that hit it.
        /// </summary>
        protected void GiveRewardsForNPC(NPC NPC)
        {
            List<PlayerDamage> playerDamageList = null;
            IBankAccount account;
            TSPlayer player;
            Money rewardMoney = 0L;

            lock (__dictionaryMutex)
            {
                if (DamageDictionary.ContainsKey(NPC))
                {
                    playerDamageList = DamageDictionary[NPC];

                    if (DamageDictionary.Remove(NPC) == false)
                    {
                        TShock.Log.ConsoleError("[SEconomy World] Removal of NPC after reward failed. This is an internal error.");
                    }
                }
            }

            if (playerDamageList == null)
            {
                return;
            }

            if (((NPC.boss && WorldConfiguration.MoneyFromBossEnabled) || (!NPC.boss && WorldConfiguration.MoneyFromNPCEnabled)) && !(NPC.SpawnedFromStatue && WorldConfiguration.IgnoreSpawnedFromStatue))
            {
                foreach (PlayerDamage damage in playerDamageList)
                {
                    if (damage.Player == null
                        || (player = TShockAPI.TShock.Players.FirstOrDefault(i => i != null && i.Index == damage.Player.whoAmI)) == null
                        || (account = Parent.GetBankAccount(player)) == null)
                    {
                        continue;
                    }

                    rewardMoney = CustomMultiplier * Convert.ToInt64(Math.Round(Convert.ToDouble(WorldConfiguration.MoneyPerDamagePoint) * damage.Damage));

                    //load override by NPC type, this allows you to put a modifier on the base for a specific mob type.
                    Configuration.WorldConfiguration.NPCRewardOverride overrideReward = WorldConfiguration.Overrides.FirstOrDefault(i => i.NPCID == NPC.type);
                    if (overrideReward != null)
                    {
                        rewardMoney = CustomMultiplier * Convert.ToInt64(Math.Round(Convert.ToDouble(overrideReward.OverridenMoneyPerDamagePoint) * damage.Damage));
                    }

                    if (rewardMoney <= 0 || player.Group.HasPermission("seconomy.world.mobgains") == false)
                    {
                        continue;
                    }

                    Journal.CachedTransaction fund = new Journal.CachedTransaction()
                    {
                        Aggregations = 1,
                        Amount = rewardMoney,
                        DestinationBankAccountK = account.BankAccountK,
                        Message = NPC.FullName,
                        SourceBankAccountK = Parent.WorldAccount.BankAccountK
                    };

                    if ((NPC.boss && WorldConfiguration.AnnounceBossKillGains) || (!NPC.boss && WorldConfiguration.AnnounceNPCKillGains))
                    {
                        fund.Options |= Journal.BankAccountTransferOptions.AnnounceToReceiver;
                    }

                    //commit it to the transaction cache
                    Parent.TransactionCache.AddCachedTransaction(fund);
                }
            }
        }

        #endregion

        /// <summary>
        /// Assigns the last player slot to a victim in PVP
        /// </summary>
        protected void PlayerHitPlayer(int HitterSlot, int VictimSlot)
        {
            lock (__pvpDictMutex)
            {
                if (PVPDamage.ContainsKey(VictimSlot))
                {
                    PVPDamage[VictimSlot] = HitterSlot;
                }
                else {
                    PVPDamage.Add(VictimSlot, HitterSlot);
                }
            }
        }

        protected Money GetDeathPenalty(TSPlayer player)
        {
            Money penalty = 0L;
            IBankAccount account;
            StaticPenaltyOverride rewardOverride;

            if (Parent == null
                || (account = Parent.GetBankAccount(player)) == null)
            {
                return default(Money);
            }

            if (WorldConfiguration.StaticDeathPenalty == false)
            {
                //The penalty defaults to a percentage of the players' current balance.
                return (long)Math.Round(Convert.ToDouble(account.Balance.Value)
                * (Convert.ToDouble(WorldConfiguration.DeathPenaltyPercentValue)
                * Math.Pow(10, -2))
                * CustomMultiplier);
            }

            penalty = WorldConfiguration.StaticPenaltyAmount;
            if ((rewardOverride = WorldConfiguration.StaticPenaltyOverrides.FirstOrDefault(i => i.TShockGroup == player.Group.Name)) != null)
            {
                penalty = CustomMultiplier * rewardOverride.StaticRewardOverride;
            }

            return penalty;
        }

        /// <summary>
        /// Runs when a player dies, and hands out penalties if enabled, and rewards for PVP
        /// </summary>
        protected void ProcessDeath(int DeadPlayerSlot, bool PVPDeath)
        {
            TSPlayer murderer = null, murdered = null;
            IBankAccount murderedAccount, murdererAccount;
            Money penalty = default(Money);
            int lastHitterSlot = default(int);
            Journal.CachedTransaction worldToPlayerTx = null,
            playerToWorldTx = null;

            //get the last hitter ID out of the dictionary
            lock (__pvpDictMutex)
            {
                if (PVPDamage.ContainsKey(DeadPlayerSlot))
                {
                    lastHitterSlot = PVPDamage[DeadPlayerSlot];
                    PVPDamage.Remove(DeadPlayerSlot);
                }
            }

            if ((murdered = TShockAPI.TShock.Players.ElementAtOrDefault(DeadPlayerSlot)) == null
                || murdered.Group.HasPermission("seconomy.world.bypassdeathpenalty") == true
                || (murderedAccount = Parent.GetBankAccount(murdered)) == null
                || (penalty = GetDeathPenalty(murdered)) == 0)
            {
                return;
            }

            playerToWorldTx = new Journal.CachedTransaction()
            {
                DestinationBankAccountK = Parent.WorldAccount.BankAccountK,
                SourceBankAccountK = murderedAccount.BankAccountK,
                Message = "dying",
                Options = Journal.BankAccountTransferOptions.MoneyTakenOnDeath | Journal.BankAccountTransferOptions.AnnounceToSender,
                Amount = penalty
            };

            //the dead player loses money unconditionally
            Parent.TransactionCache.AddCachedTransaction(playerToWorldTx);

            //but if it's a PVP death, the killer gets the losers penalty if enabled
            if (PVPDeath && WorldConfiguration.MoneyFromPVPEnabled && WorldConfiguration.KillerTakesDeathPenalty)
            {
                if ((murderer = TShockAPI.TShock.Players.ElementAtOrDefault(lastHitterSlot)) == null
                    || (murdererAccount = Parent.GetBankAccount(murderer)) == null)
                {
                    return;
                }

                worldToPlayerTx = new Journal.CachedTransaction()
                {
                    SourceBankAccountK = Parent.WorldAccount.BankAccountK,
                    DestinationBankAccountK = murdererAccount.BankAccountK,
                    Amount = penalty,
                    Message = "killing " + murdered.Name,
                    Options = Journal.BankAccountTransferOptions.AnnounceToReceiver,
                };

                Parent.TransactionCache.AddCachedTransaction(worldToPlayerTx);
            }
        }

        /// <summary>
        /// Occurs when the server has received a message from the client.
        /// </summary>
        protected void NetHooks_GetData(GetDataEventArgs args)
        {
            byte[] bufferSegment = null;
            TSPlayer player = null;

            if (args.Handled == true
                || (player = TShock.Players.ElementAtOrDefault(args.Msg.whoAmI)) == null)
            {
                return;
            }

            bufferSegment = new byte[args.Length];
            System.Array.Copy(args.Msg.readBuffer, args.Index, bufferSegment, 0, args.Length);

            if (args.MsgID == PacketTypes.NpcStrike)
            {
                NPC npc = null;
                Packets.DamageNPC dmgPacket = Packets.PacketMarshal.MarshalFromBuffer<Packets.DamageNPC>(bufferSegment);

                if (dmgPacket.NPCID < 0 || dmgPacket.NPCID > Main.npc.Length
                    || args.Msg.whoAmI < 0 || dmgPacket.NPCID > Main.player.Length)
                {
                    return;
                }

                if ((npc = Main.npc.ElementAtOrDefault(dmgPacket.NPCID)) == null)
                {
                    return;
                }

                if (DateTime.UtcNow.Subtract(player.LastThreat).TotalMilliseconds < 5000)
                {
                    return;
                }

                AddNPCDamage(npc, player.TPlayer, dmgPacket.Damage, Convert.ToBoolean(dmgPacket.CrititcalHit));
            }
        }

        /// <summary>
        /// Occurs when the server has a chunk of data to send
        /// </summary>
        protected void NetHooks_SendData(SendDataEventArgs e)
        {
            try
            {
                if (e.MsgId == PacketTypes.PlayerHurtV2)
                {
                    //occurs when a player hits another player.  ignoreClient is the player that hit, e.number is the 
                    //player that got hit, and e.number4 is a flag indicating PvP damage

                    if (Convert.ToBoolean(e.number4) && Main.player[e.number] != null)
                    {
                        PlayerHitPlayer(e.ignoreClient, e.number);
                    }
                }
                else if (e.MsgId == PacketTypes.PlayerDeathV2)
                {
                    //Occrs when the player dies.
                    ProcessDeath(e.number, Convert.ToBoolean(e.number4));
                }
            }
            catch
            {

            }
        }
    }

    /// <summary>
    /// Damage structure, wraps a player slot and the amount of damage they have done.
    /// </summary>
    class PlayerDamage
    {
        public Player Player;
        public double Damage;
    }

}

