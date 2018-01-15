﻿using GTANetworkAPI;
using WiredPlayers.model;
using WiredPlayers.database;
using WiredPlayers.house;
using WiredPlayers.business;
using WiredPlayers.chat;
using WiredPlayers.weapons;
using WiredPlayers.hooker;
using WiredPlayers.parking;
using WiredPlayers.faction;
using WiredPlayers.vehicles;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System;

namespace WiredPlayers.globals
{
    public class Globals : Script
    {
        private int fastFoodId = 1;
        public static List<FastFoodOrderModel> fastFoodOrderList;
        public static List<ClothesModel> clothesList;
        public static List<TattooModel> tattooList;
        public static List<ItemModel> itemList;
        public static List<ScoreModel> scoreList;
        public static List<AdminTicketModel> adminTicketList;
        private List<AreaModel> areaList;
        private Timer minuteTimer;
        private Timer playersCheckTimer;

        public object Houses { get; private set; }

        public Globals()
        {
            Event.OnResourceStart += onResourceStart;
            Event.OnClientEventTrigger += onClientEventTrigger;
            Event.OnPlayerDisconnected += onPlayerDisconnected;
            Event.OnEntityEnterColShape += OnEntityEnterColShapeHandler;
            Event.OnEntityExitColShape += OnEntityExitColShapeHandler;
            Event.OnPlayerEnterVehicle += onPlayerEnterVehicle;
        }

        private void onPlayerEnterVehicle(Client player, NetHandle entity, sbyte seat)
        {
            //NAPI.Native.SendNativeToPlayer(player, Hash.SET_PED_HELMET, player, false);
        }

        public List<AreaModel> getAreaList()
        {
            return areaList;
        }

        public static Client getPlayerById(int id)
        {
            Client target = null;
            foreach (Client player in NAPI.Pools.GetAllPlayers())
            {
                if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID) == id)
                {
                    target = player;
                    break;
                }
            }
            return target;
        }

        public static Vector3 getBusinessIplExit(String ipl)
        {
            Vector3 position = null;
            foreach (BusinessIplModel iplModel in Constants.BUSINESS_IPL_LIST)
            {
                if (iplModel.ipl == ipl)
                {
                    position = iplModel.position;
                    break;
                }
            }
            return position;
        }

        public static Vector3 getHouseIplExit(String ipl)
        {
            Vector3 position = null;
            foreach (HouseIplModel iplModel in Constants.HOUSE_IPL_LIST)
            {
                if (iplModel.ipl == ipl)
                {
                    position = iplModel.position;
                    break;
                }
            }
            return position;
        }
        
        private void onClientEventTrigger(Client player, string eventName, params object[] arguments)
        {
            // Creamos las variables compartidas
            BusinessItemModel businessItem = null;
            List<InventoryModel> inventory = new List<InventoryModel>();

            switch (eventName)
            {
                case "checkPlayerEventKeyStopAnim":
                    if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_ANIMATION) == false)
                    {
                        NAPI.Player.StopPlayerAnimation(player);
                    }
                    break;
                case "checkPlayerInventoryKey":
                    if (getPlayerInventoryTotal(player) > 0)
                    {
                        inventory = getPlayerInventory(player);
                        NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_SELF);
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_ITEMS_INVENTORY);
                    }
                    break;
                case "checkPlayerEventKey":
                    if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
                    {
                        // Hay que comprobar si está cerca de un cajero ATM
                        for (int i = 0; i < Constants.ATM_LIST.Count; i++)
                        {
                            if (player.Position.DistanceTo(Constants.ATM_LIST[i]) <= 1.5f)
                            {
                                NAPI.ClientEvent.TriggerClientEvent(player, "showATM");
                                return;
                            }
                        }

                        // Recorremos la lista de negocios
                        foreach (BusinessModel business in Business.businessList)
                        {
                            if (player.Position.DistanceTo(business.position) <= 1.5f && player.Dimension == business.dimension)
                            {
                                if (!Business.hasPlayerBusinessKeys(player, business) && business.locked)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_BUSINESS_LOCKED);
                                }
                                else
                                {
                                    Vector3 pos = getBusinessIplExit(business.ipl);
                                    NAPI.World.RequestIpl(business.ipl);
                                    NAPI.Entity.SetEntityPosition(player, pos);
                                    NAPI.Entity.SetEntityDimension(player, Convert.ToUInt32(business.id));
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_IPL, business.ipl);
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED, business.id);
                                }
                                return;
                            }
                            else if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED) == business.id)
                            {
                                Vector3 exitPosition = Business.getBusinessExitPoint(business.ipl);
                                if (player.Position.DistanceTo(exitPosition) < 2.5f)
                                {
                                    if (!Business.hasPlayerBusinessKeys(player, business) && business.locked)
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_BUSINESS_LOCKED);
                                    }
                                    else if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_ROBBERY_START) == true)
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_STEALING_PROGRESS);
                                    }
                                    else
                                    {
                                        NAPI.Entity.SetEntityPosition(player, business.position);
                                        NAPI.Entity.SetEntityDimension(player, business.dimension);
                                        NAPI.Data.SetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED, 0);
                                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_IPL);
                                        foreach (Client target in NAPI.Pools.GetAllPlayers())
                                        {
                                            if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) && NAPI.Data.HasEntityData(target, EntityData.PLAYER_IPL) && target != player)
                                            {
                                                if (NAPI.Data.GetEntityData(target, EntityData.PLAYER_IPL) == business.ipl)
                                                {
                                                    return;
                                                }
                                            }
                                        }
                                        NAPI.World.RemoveIpl(business.ipl);
                                    }
                                }
                                return;
                            }
                        }

                        // Recorremos la lista de casas
                        foreach (HouseModel house in House.houseList)
                        {
                            if (player.Position.DistanceTo(house.position) <= 1.5f && player.Dimension == house.dimension)
                            {
                                if (!House.hasPlayerHouseKeys(player, house) && house.locked)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_LOCKED);
                                }
                                else
                                {
                                    Vector3 pos = getHouseIplExit(house.ipl);
                                    NAPI.World.RequestIpl(house.ipl);
                                    NAPI.Entity.SetEntityPosition(player, pos);
                                    NAPI.Entity.SetEntityDimension(player, Convert.ToUInt32(house.id));
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_IPL, house.ipl);
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED, house.id);
                                }
                                return;
                            }
                            else if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED) == house.id)
                            {
                                Vector3 exitPosition = House.getHouseExitPoint(house.ipl);
                                if (player.Position.DistanceTo(exitPosition) < 2.5f)
                                {
                                    if (!House.hasPlayerHouseKeys(player, house) && house.locked)
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_LOCKED);
                                    }
                                    else if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_ROBBERY_START) == true)
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_STEALING_PROGRESS);
                                    }
                                    else
                                    {
                                        NAPI.Entity.SetEntityPosition(player, house.position);
                                        NAPI.Entity.SetEntityDimension(player, house.dimension);
                                        NAPI.Data.SetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED, 0);
                                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_IPL);
                                        foreach (Client target in NAPI.Pools.GetAllPlayers())
                                        {
                                            if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) && NAPI.Data.HasEntityData(target, EntityData.PLAYER_IPL) && target != player)
                                            {
                                                if (NAPI.Data.GetEntityData(target, EntityData.PLAYER_IPL) == house.ipl)
                                                {
                                                    return;
                                                }
                                            }
                                        }
                                        NAPI.World.RemoveIpl(house.ipl);
                                    }
                                }
                                return;
                            }
                        }

                        // Recorremos la lista de interiores
                        foreach (InteriorModel interior in Constants.INTERIOR_LIST)
                        {
                            if (player.Position.DistanceTo(interior.entrancePosition) < 1.5f)
                            {
                                NAPI.World.RequestIpl(interior.iplName);
                                NAPI.Entity.SetEntityPosition(player, interior.exitPosition);
                                return;
                            }
                            else if (player.Position.DistanceTo(interior.exitPosition) < 1.5f)
                            {
                                NAPI.Entity.SetEntityPosition(player, interior.entrancePosition);
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Miramos si está cerca de la puerta de salida
                        Vector3 lobbyExit = new Vector3(151.3791f, -1007.905f, -99f);

                        if(lobbyExit.DistanceTo(player.Position) < 1.25f)
                        {
                            // Comprobamos que tenga un personaje seleccionado
                            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_SQL_ID) == false)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_CHARACTER_SELECTED);
                            }
                            else
                            {
                                // Sacamos las variables necesarias para spawnear al jugador
                                int playerSqlId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                                String realName = NAPI.Data.GetEntityData(player, EntityData.PLAYER_NAME);
                                Vector3 spawnPosition = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SPAWN_POS);
                                Vector3 spawnRotation = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SPAWN_ROT);
                                ItemModel rightHand = getItemInEntity(playerSqlId, Constants.ITEM_ENTITY_RIGHT_HAND);
                                ItemModel leftHand = getItemInEntity(playerSqlId, Constants.ITEM_ENTITY_LEFT_HAND);

                                // Armamos al personaje
                                Weapons.givePlayerWeaponItems(player);

                                // Añadimos el objeto que tenga en la mano derecha
                                if (rightHand != null)
                                {
                                    businessItem = Business.getBusinessItemFromHash(rightHand.hash);

                                    if (businessItem == null || businessItem.type == Constants.ITEM_TYPE_WEAPON)
                                    {
                                        WeaponHash weapon = NAPI.Util.WeaponNameToModel(rightHand.hash);
                                        NAPI.Player.GivePlayerWeapon(player, weapon, rightHand.amount);
                                    }
                                    else
                                    {
                                        rightHand.objectHandle = NAPI.Object.CreateObject(UInt32.Parse(rightHand.hash), rightHand.position, new Vector3(0.0f, 0.0f, 0.0f), (byte)rightHand.dimension);
                                        NAPI.Entity.AttachEntityToEntity(rightHand.objectHandle, player, "PH_R_Hand", businessItem.position, businessItem.rotation);
                                        NAPI.Player.GivePlayerWeapon(player, WeaponHash.Unarmed, 1);
                                    }
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_RIGHT_HAND, rightHand.id);
                                }

                                // Añadimos el objeto que tenga en la mano izquierda
                                if (leftHand != null)
                                {
                                    businessItem = Business.getBusinessItemFromHash(leftHand.hash);
                                    leftHand.objectHandle = NAPI.Object.CreateObject(UInt32.Parse(leftHand.hash), leftHand.position, new Vector3(0.0f, 0.0f, 0.0f), (byte)leftHand.dimension);
                                    NAPI.Entity.AttachEntityToEntity(leftHand.objectHandle, player, "PH_L_Hand", businessItem.position, businessItem.rotation);
                                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_LEFT_HAND, leftHand.id);
                                }

                                // Añadimos la dimensión de inicio
                                if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED) > 0)
                                {
                                    int houseId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED);
                                    HouseModel house = House.getHouseById(houseId);
                                    NAPI.Entity.SetEntityDimension(player, Convert.ToUInt32(house.id));
                                    NAPI.World.RequestIpl(house.ipl);
                                }
                                else if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED) > 0)
                                {
                                    int businessId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED);
                                    BusinessModel business = Business.getBusinessById(businessId);
                                    NAPI.Entity.SetEntityDimension(player, Convert.ToUInt32(business.id));
                                    NAPI.World.RequestIpl(business.ipl);
                                }
                                else
                                {
                                    NAPI.Entity.SetEntityDimension(player, 0);
                                }

                                // Añadimos el jugador a la lista de conectados
                                for (int i = 0; i < scoreList.Count; i++)
                                {
                                    ScoreModel scoreModel = scoreList.ElementAt(i);
                                    if (scoreModel.playerName == String.Empty)
                                    {
                                        scoreModel.playerName = player.Name;
                                        scoreModel.playerPing = NAPI.Player.GetPlayerPing(player);
                                        NAPI.Data.SetEntityData(player, EntityData.PLAYER_ID, i);
                                        NAPI.Player.SetPlayerNametag(player, "[ID: " + i + "] " + player.Name);
                                        break;
                                    }
                                }

                                // Spawneamos al jugador en el mundo
                                NAPI.Player.SetPlayerName(player, realName);
                                NAPI.Entity.SetEntityPosition(player, spawnPosition);
                                NAPI.Entity.SetEntityRotation(player, spawnRotation);
                                NAPI.Entity.SetEntityInvincible(player, false);

                                // Comprobamos si está muerto
                                if(NAPI.Data.GetEntityData(player, EntityData.PLAYER_KILLED) != 0)
                                {
                                    // Ponemos al personaje en estado muerto
                                    int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);

                                    // Creamos las variables para dar el aviso
                                    Vector3 deathPosition = null;
                                    String deathPlace = String.Empty;
                                    String deathHour = DateTime.Now.ToString("h:mm:ss tt");
                                    
                                    // Miramos el lugar donde ha muerto
                                    if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED) > 0)
                                    {
                                        int houseId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED);
                                        HouseModel house = House.getHouseById(houseId);
                                        deathPosition = house.position;
                                        deathPlace = house.name;
                                    }
                                    else if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED) > 0)
                                    {
                                        int businessId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED);
                                        BusinessModel business = Business.getBusinessById(businessId);
                                        deathPosition = business.position;
                                        deathPlace = business.name;
                                    }
                                    else
                                    {
                                        deathPosition = NAPI.Entity.GetEntityPosition(player);
                                    }

                                    // Creamos el aviso y lo añadimos a la lista
                                    FactionWarningModel factionWarning = new FactionWarningModel(Constants.FACTION_EMERGENCY, playerId, deathPlace, deathPosition, -1, deathHour);
                                    Faction.factionWarningList.Add(factionWarning);

                                    // Creamos el mensaje de aviso
                                    String warnMessage = String.Format(Messages.INF_EMERGENCY_WARNING, Faction.factionWarningList.Count - 1);

                                    // Damos el aviso a todos los médicos de servicio
                                    foreach (Client target in NAPI.Pools.GetAllPlayers())
                                    {
                                        if (NAPI.Data.GetEntityData(target, EntityData.PLAYER_FACTION) == Constants.FACTION_EMERGENCY && NAPI.Data.GetEntityData(target, EntityData.PLAYER_ON_DUTY) == 0)
                                        {
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + warnMessage);
                                        }
                                    }

                                    //NAPI.Native.SendNativeToPlayer(player, Hash._RESET_LOCALPLAYER_STATE, player);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash.RESET_PLAYER_ARREST_STATE, player);

                                    //NAPI.Native.SendNativeToPlayer(player, Hash.IGNORE_NEXT_RESTART, true);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash._DISABLE_AUTOMATIC_RESPAWN, true);

                                    //NAPI.Native.SendNativeToPlayer(player, Hash.SET_FADE_IN_AFTER_DEATH_ARREST, true);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash.SET_FADE_OUT_AFTER_DEATH, false);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash.NETWORK_REQUEST_CONTROL_OF_ENTITY, player);

                                    //NAPI.Native.SendNativeToPlayer(player, Hash.FREEZE_ENTITY_POSITION, player, false);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash.NETWORK_RESURRECT_LOCAL_PLAYER, player.Position.X, player.Position.Y, player.Position.Z, player.Rotation.Z, false, false);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash.RESURRECT_PED, player);

                                    //NAPI.Native.SendNativeToPlayer(player, Hash.SET_PED_CAN_RAGDOLL, player, true);
                                    //NAPI.Native.SendNativeToPlayer(player, Hash.SET_PED_TO_RAGDOLL, player, -1, -1, 0, false, false, false);
                                    
                                    NAPI.Entity.SetEntityInvincible(player, true);
                                    NAPI.Data.SetEntityData(player, EntityData.TIME_HOSPITAL_RESPAWN, getTotalSeconds() + 240);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_EMERGENCY_WARN);
                                }

                                // Activamos el flag de jugador conectado
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_PLAYING, true);
                            }
                        }
                    }
                    break;
                case "processMenuAction":
                    String message = String.Empty;
                    int itemId = (int)arguments[0];
                    ItemModel item = getItemModelFromId(itemId);
                    businessItem = Business.getBusinessItemFromHash(item.hash);

                    switch (arguments[1].ToString().ToLower())
                    {
                        case "consumir":
                            item.amount--;
                            Database.updateItem(item);
                            message = String.Format(Messages.INF_PLAYER_INVENTORY_CONSUME, businessItem.description.ToLower());
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);

                            // Miramos si sube el nivel de alcohol
                            if (businessItem.alcoholLevel > 0)
                            {
                                float currentAlcohol = 0;
                                if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_DRUNK_LEVEL) == true)
                                {
                                    currentAlcohol = NAPI.Data.GetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL);
                                }
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL, currentAlcohol + businessItem.alcoholLevel);

                                // Miramos si ha excedido el nivel de alcohol
                                if (currentAlcohol + businessItem.alcoholLevel > Constants.WASTED_LEVEL)
                                {
                                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_WALKING_STYLE, "move_m@drunk@verydrunk");
                                    NAPI.ClientEvent.TriggerClientEventForAll("changePlayerWalkingStyle", player.Handle, "move_m@drunk@verydrunk");
                                }
                            }

                            // Miramos si cambia la vida
                            if(businessItem.health != 0)
                            {
                                int health = NAPI.Player.GetPlayerHealth(player);
                                NAPI.Player.SetPlayerHealth(player, health + businessItem.health);
                            }

                            // Comprobamos si era el último
                            if (item.amount == 0)
                            {
                                Database.removeItem(item.id);
                                itemList.Remove(item);
                            }

                            // Actualizamos el inventario
                            inventory = getPlayerInventory(player);
                            NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_SELF);
                            break;
                        case "abrir":
                            // Miramos qué objeto hemos abierto
                            switch (item.hash)
                            {
                                case Constants.ITEM_HASH_PACK_BEER_AM:
                                    ItemModel itemModel = getPlayerItemModelFromHash(NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID), Constants.ITEM_HASH_BOTTLE_BEER_AM);
                                    if (itemModel == null)
                                    {
                                        // El jugador no tiene el objeto, lo creamos
                                        itemModel = new ItemModel();
                                        itemModel.hash = Constants.ITEM_HASH_BOTTLE_BEER_AM;
                                        itemModel.ownerEntity = Constants.ITEM_ENTITY_PLAYER;
                                        itemModel.ownerIdentifier = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                                        itemModel.amount = Constants.ITEM_OPEN_BEER_AMOUNT;
                                        itemModel.position = new Vector3(0.0f, 0.0f, 0.0f);
                                        itemModel.dimension = player.Dimension;
                                        itemModel.id = Database.addNewItem(itemModel);

                                        // Añadimos el objeto a la lista
                                        itemList.Add(itemModel);
                                    }
                                    else
                                    {
                                        // El jugador ya tiene el objeto, le añadimos la cantidad
                                        itemModel.amount += Constants.ITEM_OPEN_BEER_AMOUNT;
                                        Database.updateItem(item);
                                    }
                                    break;
                            }

                            // Restamos uno a la cantidad de objetos contenedores
                            substractPlayerItems(item);

                            // Mandamos el aviso al jugador
                            message = String.Format(Messages.INF_PLAYER_INVENTORY_OPEN, businessItem.description.ToLower());
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);

                            // Actualizamos el inventario
                            inventory = getPlayerInventory(player);
                            NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_SELF);
                            break;
                        case "equipar":
                            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_RIGHT_HAND_OCCUPIED);
                            }
                            else
                            {
                                // Establecemos el objeto en la mano
                                item.ownerEntity = Constants.ITEM_ENTITY_RIGHT_HAND;
                                item.objectHandle = NAPI.Object.CreateObject(UInt32.Parse(item.hash), item.position, new Vector3(0.0f, 0.0f, 0.0f), (byte)player.Dimension);
                                NAPI.Entity.AttachEntityToEntity(item.objectHandle, player, "PH_R_Hand", businessItem.position, businessItem.rotation);
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_RIGHT_HAND, itemId);

                                // Mandamos el mensaje
                                message = String.Format(Messages.INF_PLAYER_INVENTORY_EQUIP, businessItem.description.ToLower());
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                            }
                            break;
                        case "tirar":
                            // Quitamos una unidad del inventario
                            item.amount--;
                            Database.updateItem(item);

                            // Miramos si hay más objetos en el suelo
                            ItemModel closestItem = getClosestItemWithHash(player, item.hash);
                            if (closestItem != null)
                            {
                                closestItem.amount++;
                                Database.updateItem(item);
                            }
                            else
                            {
                                closestItem = item.Copy();
                                closestItem.amount = 1;
                                closestItem.ownerEntity = Constants.ITEM_ENTITY_GROUND;
                                closestItem.dimension = player.Dimension;
                                closestItem.position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z - 0.8f);
                                closestItem.objectHandle = NAPI.Object.CreateObject(UInt32.Parse(closestItem.hash), closestItem.position, new Vector3(0.0f, 0.0f, 0.0f), (byte)closestItem.dimension);
                                closestItem.id = Database.addNewItem(closestItem);
                                itemList.Add(closestItem);
                            }

                            // Comprobamos si era el último
                            if (item.amount == 0)
                            {
                                Database.removeItem(item.id);
                                itemList.Remove(item);
                            }

                            // Mandamos el mensaje
                            message = String.Format(Messages.INF_PLAYER_INVENTORY_DROP, businessItem.description.ToLower());
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);

                            // Actualizamos el inventario
                            inventory = getPlayerInventory(player);
                            NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_SELF);
                            break;
                        case "requisar":
                            // Obtenemos el jugador objetivo
                            Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SEARCHED_TARGET);

                            // Traspasamos el objeto al jugador
                            item.ownerEntity = Constants.ITEM_ENTITY_PLAYER;
                            item.ownerIdentifier = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                            Database.updateItem(item);

                            // Actualizamos el inventario
                            inventory = getPlayerInventoryAndWeapons(target);
                            NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_PLAYER);

                            // Enviamos el mensaje
                            String playerMessage = String.Format(Messages.INF_POLICE_RETIRED_ITEMS_TO, target.Name);
                            String targetMessage = String.Format(Messages.INF_POLICE_RETIRED_ITEMS_FROM, player.Name);
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerMessage);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetMessage);
                            break;
                        case "guardar":
                            // Obtenemos el vehículo objetivo
                            NetHandle targetVehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_OPENED_TRUNK);

                            // Traspasamos el objeto al jugador
                            item.ownerEntity = Constants.ITEM_ENTITY_VEHICLE;
                            item.ownerIdentifier = NAPI.Data.GetEntityData(targetVehicle, EntityData.VEHICLE_ID);
                            Database.updateItem(item);

                            // Si tiene un arma, se la quitamos
                            foreach (WeaponHash weapon in NAPI.Player.GetPlayerWeapons(player))
                            {
                                if (weapon.ToString() == item.hash)
                                {
                                    NAPI.Player.RemovePlayerWeapon(player, weapon);
                                    break;
                                }
                            }

                            // Actualizamos el inventario
                            inventory = getPlayerInventoryAndWeapons(player);
                            NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_VEHICLE_PLAYER);

                            // Enviamos el mensaje
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_TRUNK_STORED_ITEMS);
                            break;
                        case "sacar":
                            // Obtenemos el vehículo objetivo
                            NetHandle sourceVehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_OPENED_TRUNK);

                            // Traspasamos el objeto al jugador
                            WeaponHash weaponHash = NAPI.Util.WeaponNameToModel(item.hash);
                            if (weaponHash != 0)
                            {
                                // Es un arma, se la damos al jugador
                                item.ownerEntity = Constants.ITEM_ENTITY_WHEEL;
                                NAPI.Player.GivePlayerWeapon(player, weaponHash, 0);
                                NAPI.Player.SetPlayerWeaponAmmo(player, weaponHash, item.amount);
                            }
                            else
                            {
                                // Es un objeto, lo colocamos en el inventario
                                item.ownerEntity = Constants.ITEM_ENTITY_PLAYER;
                            }

                            item.ownerIdentifier = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                            Database.updateItem(item);

                            // Actualizamos el inventario
                            inventory = getVehicleTrunkInventory(sourceVehicle);
                            NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_VEHICLE_TRUNK);

                            // Enviamos el mensaje
                            Chat.sendMessageToNearbyPlayers(player, Messages.INF_TRUNK_ITEM_WITHDRAW, Constants.MESSAGE_ME, 20.0f);
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_TRUNK_WITHDRAW_ITEMS);
                            break;
                    }
                    break;
                case "closeVehicleTrunk":
                    // Cerramos el maletero del vehículo
                    NetHandle vehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_OPENED_TRUNK);
                    NAPI.Vehicle.SetVehicleDoorState(vehicle, Constants.VEHICLE_TRUNK, false);

                    // Quitamos el link con el vehículo
                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_OPENED_TRUNK);
                    break;
                case "getPlayerTattoos":
                    NetHandle playerHandle = (NetHandle)arguments[0];
                    Client targetPlayer = NAPI.Player.GetPlayerFromHandle(playerHandle);
                    int targetId = NAPI.Data.GetEntityData(targetPlayer, EntityData.PLAYER_SQL_ID);
                    List<TattooModel> playerTattooList = getPlayerTattoos(targetId);
                    NAPI.ClientEvent.TriggerClientEvent(player, "updatePlayerTattoos", NAPI.Util.ToJson(playerTattooList), targetPlayer);
                    break;
            }
        }

        private void onResourceStart()
        {
            areaList = new List<AreaModel>();
            scoreList = new List<ScoreModel>();
            adminTicketList = new List<AdminTicketModel>();
            fastFoodOrderList = new List<FastFoodOrderModel>();

            // Área para cambiar de personaje en el lobby
            ColShape characterSelectionRectangle = NAPI.ColShape.Create2DColShape(151.25f, -1002.18f, 1.8f, 2.5f);
            AreaModel characterSelectionArea = new AreaModel("character-selector", characterSelectionRectangle);
            NAPI.TextLabel.CreateTextLabel("Pulsa F para cambiar de personaje", new Vector3(152.2911f, -1001.088f, -99f), 20.0f, 0.75f, 0, new Color(255, 255, 255), false, 0);
            areaList.Add(characterSelectionArea);

            // Añadimos el interior del concesionario
            NAPI.World.RequestIpl("shr_int");
            NAPI.World.RequestIpl("shr_int_lod");
            NAPI.World.RemoveIpl("fakeint");
            NAPI.World.RemoveIpl("fakeint_lod");
            NAPI.World.RemoveIpl("fakeint_boards");
            NAPI.World.RemoveIpl("fakeint_boards_lod");
            NAPI.World.RemoveIpl("shutter_closed");

            // Añadimos la puerta del clubhouse para evitar que caigan players al vacío
            NAPI.World.RequestIpl("hei_bi_hw1_13_door");

            // Área para abrir las puertas principales del concesionario
            ColShape motorSportRectangle = NAPI.ColShape.Create2DColShape(-62.61f, -1094.54f, 4.69f, 2.71f);
            AreaModel motorSportArea = new AreaModel("motorsport-main-doors", motorSportRectangle);
            //NAPI.RegisterCustomColShape(motorSportRectangle);
            areaList.Add(motorSportArea);

            // Área para abrir las puertas del parking del concesionario
            ColShape motorSportParkingRectangle = NAPI.ColShape.Create2DColShape(-40.63f, -1110.12f, 4.02f, 4.02f);
            AreaModel motorSportParkingArea = new AreaModel("motorsport-parking-doors", motorSportParkingRectangle);
            areaList.Add(motorSportParkingArea);

            // Área para bloquear puertas de negocios
            ColShape supermarketRectangle = NAPI.ColShape.Create2DColShape(-711.5449f, -915.5397f, 204.56f, 204.25f);
            AreaModel supermarketArea = new AreaModel("supermarket", supermarketRectangle);
            areaList.Add(supermarketArea);

            ColShape clubhouseRectangle = NAPI.ColShape.Create2DColShape(981.7533f, -102.7987f, 877f, 881.1f);
            AreaModel clubhouseArea = new AreaModel("clubhouse", clubhouseRectangle);
            areaList.Add(clubhouseArea);

            ColShape vanillaRectangle = NAPI.ColShape.Create2DColShape(127.9552f, -1298.503f, 1171.7653f, 1167.0788f);
            AreaModel vanillaArea = new AreaModel("vanilla", vanillaRectangle);
            areaList.Add(vanillaArea);

            // Área para condenar a jugadores
            ColShape jailRectangle = NAPI.ColShape.Create2DColShape(458.8354f, -992.3193f, 6.3419f, 4.65f);
            AreaModel jailArea = new AreaModel("jail", jailRectangle);
            areaList.Add(jailArea);

            // Área para los vestuarios del LSPD
            ColShape lspdRoomLockersRectangle = NAPI.ColShape.Create2DColShape(455.3337f, -990.8022f, 527.39f, 544.29f);
            AreaModel roomLockersArea = new AreaModel("lockerslspd", lspdRoomLockersRectangle);
            areaList.Add(roomLockersArea);

            for (int i = 0; i < NAPI.Server.GetMaxPlayers(); i++)
            {
                ScoreModel scoreModel = new ScoreModel(i, String.Empty, 0);
                scoreList.Add(scoreModel);
            }

            foreach (InteriorModel interior in Constants.INTERIOR_LIST)
            {
                if (interior.blipId > 0)
                {
                    interior.blip = NAPI.Blip.CreateBlip(interior.entrancePosition);
                    NAPI.Blip.SetBlipSprite(interior.blip, interior.blipId);
                    NAPI.Blip.SetBlipName(interior.blip, interior.blipName);
                    NAPI.Blip.SetBlipShortRange(interior.blip, true);
                }

                if (interior.captionMessage != String.Empty)
                {
                    interior.textLabel = NAPI.TextLabel.CreateTextLabel(interior.captionMessage, interior.entrancePosition, 20.0f, 0.75f, 0, new Color(255, 255, 255), false, 0);
                }
            }

            // Definimos variables globales del servidor
            Random rnd = new Random();
            //NAPI.SetWorldSharedData(EntityData.JOB_ORDERS_TIME, getTotalSeconds() + rnd.Next(0, 1) * 60);

            // Añadimos la lista de pedidos de comida rápida
            //NAPI.SetWorldSharedData(EntityData.FASTFOOD_LIST, NAPI.Util.ToJson(fastFoodOrderList));

            // Creamos los timers cíclicos
            playersCheckTimer = new Timer(UpdatePlayerList, null, 500, 500);
            minuteTimer = new Timer(OnMinuteSpent, null, 60000, 60000);
        }

        private void OnEntityEnterColShapeHandler(ColShape shape, NetHandle entity)
        {
            if (NAPI.Entity.GetEntityType(entity) == EntityType.Player)
            {
                Client player = NAPI.Player.GetPlayerFromHandle(entity);
                foreach (AreaModel area in areaList)
                {
                    if (area.area == shape)
                    {
                        switch (area.action)
                        {
                            case "character-selector":
                                if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == false)
                                {
                                    NAPI.Data.SetEntitySharedData(player, "create-area", true);
                                }
                                break;
                            case "motorsport-main-doors":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 1417577297, -60.54582f, -1094.749f, 26.88872f, false, 0f, false);
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 2059227086, -59.89302f, -1092.952f, 26.88362f, false, 0f, false);
                                break;
                            case "motorsport-parking-doors":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 1417577297, -37.33113f, -1108.873f, 26.7198f, false, 0f, false);
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 2059227086, -39.13366f, -1108.218f, 26.7198f, false, 0f, false);
                                break;
                            case "jail":
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_JAIL_AREA, true);
                                break;
                            case "supermarket":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 2065277225, -711.5449f, -915.5397f, 19.21559f, true, 0f, false);
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, -868672903, -711.5449f, -915.5397f, 19.21559f, true, 0f, false);
                                break;
                            case "clubhouse":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 190770132, 981.7533f, -102.7987f, 74.84873f, true, 0f, false);
                                break;
                            case "vanilla":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, -1116041313, 127.9552f, -1298.503f, 29.41962f, true, 0f, false);
                                break;
                            case "lockerslspd":
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_IN_LSPD_ROOM_LOCKERS_AREA, true);
                                break;
                        }
                    }
                }
            }
        }

        private void OnEntityExitColShapeHandler(ColShape shape, NetHandle entity)
        {
            if (NAPI.Entity.GetEntityType(entity) == EntityType.Player)
            {
                Client player = NAPI.Player.GetPlayerFromHandle(entity);
                foreach (AreaModel area in areaList)
                {
                    if (area.area == shape)
                    {
                        switch (area.action)
                        {
                            case "character-selector":
                                if (NAPI.Data.HasEntityData(entity, EntityData.PLAYER_PLAYING) == false)
                                {
                                    NAPI.Data.SetEntitySharedData(entity, "create-area", false);
                                }
                                break;
                            case "motorsport-main-doors":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 1417577297, -60.54582f, -1094.749f, 26.88872f, true, 0f, false);
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 2059227086, -59.89302f, -1092.952f, 26.88362f, true, 0f, false);
                                break;
                            case "motorsport-parking-doors":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 1417577297, -37.33113f, -1108.873f, 26.7198f, true, 0f, false);
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 2059227086, -39.13366f, -1108.218f, 26.7198f, true, 0f, false);
                                break;
                            case "supermarket":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 2065277225, -711.5449f, -915.5397f, 19.21559f, true, 0f, false);
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, -868672903, -711.5449f, -915.5397f, 19.21559f, true, 0f, false);
                                break;
                            case "clubhouse":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 190770132, 981.7533f, -102.7987f, 74.84873f, true, 0f, false);
                                break;
                            case "vanilla":
                                //NAPI.Native.SendNativeToPlayer(player, Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, -1116041313, 127.9552f, -1298.503f, 29.41962f, true, 0f, false);
                                break;
                            case "jail":
                                NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JAIL_AREA);
                                break;
                            case "lockerslspd":
                                NAPI.Data.ResetEntityData(player, EntityData.PLAYER_IN_LSPD_ROOM_LOCKERS_AREA);
                                break;
                        }
                    }
                }
            }
        }

        private void onPlayerDisconnected(Client player, byte type, string reason)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
            {
                // Se elimina el personaje de la lista de conectados
                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);
                ScoreModel scoreModel = scoreList.ElementAt(playerId);
                scoreModel.playerName = String.Empty;
                scoreModel.playerPing = 0;

                // Guardamos los datos del personaje
                PlayerModel character = new PlayerModel();

                // Datos no sincronizados
                character.position = NAPI.Entity.GetEntityPosition(player);
                character.rotation = NAPI.Entity.GetEntityRotation(player);
                character.health = NAPI.Player.GetPlayerHealth(player);
                character.armor = NAPI.Player.GetPlayerArmor(player);
                character.id = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                character.phone = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PHONE);
                character.radio = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RADIO);
                character.killed = NAPI.Data.GetEntityData(player, EntityData.PLAYER_KILLED);
                character.faction = NAPI.Data.GetEntityData(player, EntityData.PLAYER_FACTION);
                character.job = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB);
                character.rank = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RANK);
                character.duty = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ON_DUTY);
                character.carKeys = NAPI.Data.GetEntityData(player, EntityData.PLAYER_VEHICLE_KEYS);
                character.documentation = NAPI.Data.GetEntityData(player, EntityData.PLAYER_DOCUMENTATION);
                character.licenses = NAPI.Data.GetEntityData(player, EntityData.PLAYER_LICENSES);
                character.insurance = NAPI.Data.GetEntityData(player, EntityData.PLAYER_MEDICAL_INSURANCE);
                character.weaponLicense = NAPI.Data.GetEntityData(player, EntityData.PLAYER_WEAPON_LICENSE);
                character.houseRent = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RENT_HOUSE);
                character.houseEntered = NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED);
                character.businessEntered = NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED);
                character.employeeCooldown = NAPI.Data.GetEntityData(player, EntityData.PLAYER_EMPLOYEE_COOLDOWN);
                character.jobCooldown = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COOLDOWN);
                character.jobDeliver = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_DELIVER);
                character.jobPoints = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_POINTS);
                character.played = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PLAYED);
                character.jailed = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JAIL_TYPE) + "," + NAPI.Data.GetEntityData(player, EntityData.PLAYER_JAILED);

                // Datos sincronizados
                character.money = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);
                character.bank = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK);

                // Guardamos en la base de datos
                Database.saveCharacterInformation(character);

                if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
                {
                    int itemId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                    ItemModel item = getItemModelFromId(itemId);
                    NAPI.Entity.DetachEntity(item.objectHandle);
                    NAPI.Entity.DeleteEntity(item.objectHandle);
                }

                // Borramos la duda que tenga abierta
                foreach (AdminTicketModel adminTicket in adminTicketList)
                {
                    if (adminTicket.playerId == playerId)
                    {
                        adminTicketList.Remove(adminTicket);
                        break;
                    }
                }
            }
        }
        /*
        private void OnChatCommandHandler(Client player, String command, CancelEventArgs e)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
            {
                NAPI.Util.ConsoleOutput(player.Name + " ha usado el comando '" + command + "'");
            }
            else if(command.StartsWith("/login") == false)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_CANT_COMMAND);
                e.Cancel = true;
            }
        }*/

        public static NetHandle getClosestVehicle(Client player, float distance = 2.5f)
        {
            NetHandle vehicle = new NetHandle();
            foreach (NetHandle veh in NAPI.Pools.GetAllVehicles())
            {
                Vector3 vehPos = NAPI.Entity.GetEntityPosition(veh);
                uint vehicleDimension = NAPI.Entity.GetEntityDimension(veh);
                float distanceVehicleToPlayer = player.Position.DistanceTo(vehPos);
                if (distanceVehicleToPlayer < distance && player.Dimension == vehicleDimension)
                {
                    distance = distanceVehicleToPlayer;
                    vehicle = veh;

                }
            }
            return vehicle;
        }

        public static int getTotalSeconds()
        {
            return (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private void UpdatePlayerList(object unused)
        {
            try
            {
                // Actualizamos la lista de jugadores
                foreach (Client player in NAPI.Pools.GetAllPlayers())
                {
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
                    {
                        int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);
                        ScoreModel scoreModel = scoreList.ElementAt(playerId);
                        scoreModel.playerPing = NAPI.Player.GetPlayerPing(player);
                    }
                }
            }
            catch (Exception ex)
            {
                NAPI.Util.ConsoleOutput("[EXCEPTION UpdatePlayerList] " + ex.Message);
            }
        }

        private void OnMinuteSpent(object unused)
        {
            try
            {
                // Ajustamos la hora del servidor
                TimeSpan currentTime = TimeSpan.FromTicks(DateTime.Now.Ticks);
                //NAPI.SetTime(currentTime.Hours, currentTime.Minutes, currentTime.Seconds);

                int totalSeconds = getTotalSeconds();
                foreach (Client player in NAPI.Pools.GetAllPlayers())
                {
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
                    {
                        int played = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PLAYED);
                        if (played > 0 && played % 60 == 0)
                        {
                            // Reducimos el tiempo entre empleos
                            int employeeCooldown = NAPI.Data.GetEntityData(player, EntityData.PLAYER_EMPLOYEE_COOLDOWN);
                            if(employeeCooldown > 0)
                            {
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_EMPLOYEE_COOLDOWN, employeeCooldown - 1);
                            }

                            // Generamos la paga
                            generatePlayerPayday(player);
                        }
                        NAPI.Data.SetEntityData(player, EntityData.PLAYER_PLAYED, played + 1);

                        // Miramos si está muerto y esperando a ir al hospital
                        if (NAPI.Data.HasEntityData(player, EntityData.TIME_HOSPITAL_RESPAWN) == true)
                        {
                            if (NAPI.Data.GetEntityData(player, EntityData.TIME_HOSPITAL_RESPAWN) <= totalSeconds)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_PLAYER_CAN_DIE);
                            }
                        }

                        // Miramos si tiene tiempo de descanso de trabajo
                        int jobCooldown = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COOLDOWN);
                        if(jobCooldown > 0)
                        {
                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_COOLDOWN, jobCooldown - 1);
                        }

                        // Miramos si está encarcelado
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JAILED) == true)
                        {
                            int jailTime = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JAILED);
                            if (jailTime == 1)
                            {
                                // Miramos dónde spawnear
                                if(NAPI.Data.GetEntityData(player, EntityData.PLAYER_JAIL_TYPE) == Constants.JAIL_TYPE_IC)
                                {
                                    NAPI.Entity.SetEntityPosition(player, Constants.JAIL_SPAWNS[3]);
                                }
                                else
                                {
                                    NAPI.Entity.SetEntityPosition(player, Constants.JAIL_SPAWNS[4]);
                                }

                                // Eliminamos la cárcel para el jugador
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_JAILED, 0);
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_JAIL_TYPE, 0);

                                // Mandamos el mensaje al jugador
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_PLAYER_UNJAILED);
                            }
                            else if(jailTime > 0)
                            {
                                jailTime--;
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_JAILED, jailTime);
                            }
                        }
                        
                        // Bajamos el nivel de alcohol
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_DRUNK_LEVEL) == true)
                        {
                            // Calculamos el nivel de alcohol
                            float drunkLevel = NAPI.Data.GetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL) - 0.05f;

                            if(drunkLevel <= 0.0f)
                            {
                                NAPI.Data.ResetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL);
                            }
                            else
                            {
                                // Miramos si ha bajado del límite de alcohol
                                if(drunkLevel < Constants.WASTED_LEVEL)
                                {
                                    NAPI.Data.ResetEntitySharedData(player, EntityData.PLAYER_WALKING_STYLE);
                                    NAPI.ClientEvent.TriggerClientEventForAll("resetPlayerWalkingStyle", player.Handle);
                                }

                                // Cambiamos el nivel de alcohol
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL, drunkLevel);
                            }
                        }

                        // Guardamos el personaje
                        PlayerModel character = new PlayerModel();

                        // Lista de datos no sincronizados
                        character.position = NAPI.Entity.GetEntityPosition(player);
                        character.rotation = NAPI.Entity.GetEntityRotation(player);
                        character.health = NAPI.Player.GetPlayerHealth(player);
                        character.armor = NAPI.Player.GetPlayerArmor(player);
                        character.id = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                        character.phone = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PHONE);
                        character.radio = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RADIO);
                        character.killed = NAPI.Data.GetEntityData(player, EntityData.PLAYER_KILLED);
                        character.faction = NAPI.Data.GetEntityData(player, EntityData.PLAYER_FACTION);
                        character.job = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB);
                        character.rank = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RANK);
                        character.duty = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ON_DUTY);
                        character.carKeys = NAPI.Data.GetEntityData(player, EntityData.PLAYER_VEHICLE_KEYS);
                        character.documentation = NAPI.Data.GetEntityData(player, EntityData.PLAYER_DOCUMENTATION);
                        character.licenses = NAPI.Data.GetEntityData(player, EntityData.PLAYER_LICENSES);
                        character.insurance = NAPI.Data.GetEntityData(player, EntityData.PLAYER_MEDICAL_INSURANCE);
                        character.weaponLicense = NAPI.Data.GetEntityData(player, EntityData.PLAYER_WEAPON_LICENSE);
                        character.houseRent = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RENT_HOUSE);
                        character.houseEntered = NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED);
                        character.businessEntered = NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED);
                        character.employeeCooldown = NAPI.Data.GetEntityData(player, EntityData.PLAYER_EMPLOYEE_COOLDOWN);
                        character.jobCooldown = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COOLDOWN);
                        character.jobDeliver = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_DELIVER);
                        character.jobPoints = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_POINTS);
                        character.played = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PLAYED);
                        character.jailed = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JAIL_TYPE) + "," + NAPI.Data.GetEntityData(player, EntityData.PLAYER_JAILED);

                        // Lista de datos sincronizados
                        character.money = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);
                        character.bank = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK);

                        // Guardado del personaje en base de datos
                        Database.saveCharacterInformation(character);
                    }
                }

                // Generación de nuevos pedidos en los trabajos
                /*if (NAPI.GetWorldSharedData(EntityData.JOB_ORDERS_TIME) <= totalSeconds)
                {
                    Random rnd = new Random();
                    int generatedOrders = rnd.Next(7, 20);
                    for (int i = 0; i < generatedOrders; i++)
                    {
                        FastFoodOrderModel order = new FastFoodOrderModel();
                        order.id = fastFoodId;
                        order.pizzas = rnd.Next(0, 4);
                        order.hamburgers = rnd.Next(0, 4);
                        order.sandwitches = rnd.Next(0, 4);
                        order.limit = totalSeconds + 300;
                        order.taken = false;
                        fastFoodOrderList.Add(order);
                        fastFoodId++;
                    }
                    //NAPI.SetWorldSharedData(EntityData.JOB_ORDERS_TIME, totalSeconds + rnd.Next(2, 5) * 60);
                }*/

                // Borrados de pedidos de comida rápida caducados
                fastFoodOrderList.RemoveAll(order => !order.taken && order.limit <= totalSeconds);

                // Añadimos la lista de pedidos de comida rápida
                //NAPI.SetWorldSharedData(EntityData.FASTFOOD_LIST, NAPI.Util.ToJson(fastFoodOrderList));

                // Guardamos los vehículos
                List<VehicleModel> vehicleList = new List<VehicleModel>();

                foreach (NetHandle vehicle in NAPI.Pools.GetAllVehicles())
                {
                    if (!NAPI.Data.HasEntityData(vehicle, EntityData.VEHICLE_TESTING) && NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_FACTION) == 0)
                    {
                        // Obtenemos los colores del vehículo
                        Color primaryColor = NAPI.Vehicle.GetVehicleCustomPrimaryColor(vehicle);
                        Color secondaryColor = NAPI.Vehicle.GetVehicleCustomSecondaryColor(vehicle);

                        // Obtenemos los valores necesarios para recrear el vehículo
                        VehicleModel vehicleModel = new VehicleModel();
                        vehicleModel.id = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_ID);
                        vehicleModel.model = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_MODEL);
                        vehicleModel.position = NAPI.Entity.GetEntityPosition(vehicle);
                        vehicleModel.rotation = NAPI.Entity.GetEntityRotation(vehicle);
                        vehicleModel.dimension = NAPI.Entity.GetEntityDimension(vehicle);
                        vehicleModel.colorType = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_COLOR_TYPE);
                        vehicleModel.firstColor = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_FIRST_COLOR);
                        vehicleModel.secondColor = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_SECOND_COLOR);
                        vehicleModel.pearlescent = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PEARLESCENT_COLOR);
                        vehicleModel.faction = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_FACTION);
                        vehicleModel.plate = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PLATE);
                        vehicleModel.owner = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_OWNER);
                        vehicleModel.price = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PRICE);
                        vehicleModel.parking = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PARKING);
                        vehicleModel.parked = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PARKED);
                        vehicleModel.gas = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_GAS);
                        vehicleModel.kms = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_KMS);

                        // Añadimos el vehículo a la lista
                        vehicleList.Add(vehicleModel);
                    }
                }

                // Guardamos la lista de vehículos
                Database.saveAllVehicles(vehicleList);
            }
            catch (Exception ex)
            {
                NAPI.Util.ConsoleOutput("[EXCEPTION OnMinuteSpent] " + ex.Message);
            }
        }

        private void generatePlayerPayday(Client player)
        {
            int total = 0;
            int bank = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK);
            int playerJob = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB);
            int playerRank = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RANK);
            int playerFaction = NAPI.Data.GetEntityData(player, EntityData.PLAYER_FACTION);
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + "Resumen de movimientos en la cuenta bancaria");

            // Generación del sueldo
            if (playerFaction > 0 && playerFaction <= Constants.LAST_STATE_FACTION)
            {
                foreach (FactionModel faction in Constants.FACTION_RANK_LIST)
                {
                    if (faction.faction == playerFaction && faction.rank == playerRank)
                    {
                        total += faction.salary;
                        break;
                    }
                }
            }
            else
            {
                foreach (JobModel job in Constants.JOB_LIST)
                {
                    if (job.job == playerJob)
                    {
                        total += job.salary;
                        break;
                    }
                }
            }
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Sueldo: " + total + "$");

            // Ingresos extra por nivel
            int levelEarnings = getPlayerLevel(player) * Constants.PAID_PER_LEVEL;
            total += levelEarnings;
            if(levelEarnings > 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Ingresos extra: " + levelEarnings + "$");
            }

            // Los bancos nos regalan dinerico
            int bankInterest = (int)Math.Round(bank * 0.001);
            total += bankInterest;
            if (bankInterest > 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Intereses bancarios: " + bankInterest + "$");
            }

            // Generación de impuestos por vehículos
            foreach (NetHandle vehicle in NAPI.Pools.GetAllVehicles())
            {
                VehicleHash vehicleHass = (VehicleHash)NAPI.Entity.GetEntityModel(vehicle);
                if (NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_OWNER) == player.Name && NAPI.Vehicle.GetVehicleClass(vehicleHass) != Constants.VEHICLE_CLASS_CYCLES)
                {
                    int vehicleTaxes = (int)Math.Round(NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PRICE) * Constants.TAXES_VEHICLE);
                    int vehicleId = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_ID);
                    String vehicleModel = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_MODEL);
                    String vehiclePlate = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PLATE) == String.Empty ? "LS " + (1000 + vehicleId) : NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PLATE);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Impuestos de " + vehicleModel + " (" + vehiclePlate + "): -" + vehicleTaxes + "$");
                    total -= vehicleTaxes;
                }
            }

            // Generación de impuestos por vehículos
            foreach (ParkedCarModel parkedCar in Parking.parkedCars)
            {
                VehicleHash vehicleHass = NAPI.Util.VehicleNameToModel(parkedCar.vehicle.model);
                if (parkedCar.vehicle.owner == player.Name && NAPI.Vehicle.GetVehicleClass(vehicleHass) != Constants.VEHICLE_CLASS_CYCLES)
                {
                    int vehicleTaxes = (int)Math.Round(parkedCar.vehicle.price * Constants.TAXES_VEHICLE);
                    String vehiclePlate = parkedCar.vehicle.plate == String.Empty ? "LS " + (1000 + parkedCar.vehicle.id) : parkedCar.vehicle.plate;
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Impuestos de " + parkedCar.vehicle.model + " (" + vehiclePlate + "): -" + vehicleTaxes + "$");
                    total -= vehicleTaxes;
                }
            }

            // Generación de impuestos por viviendas
            foreach (HouseModel house in House.houseList)
            {
                if (house.owner == player.Name)
                {
                    int houseTaxes = (int)Math.Round(house.price * Constants.TAXES_HOUSE);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Impuestos de la vivienda " + house.name + ": -" + houseTaxes + "$");
                    total -= houseTaxes;
                }
            }

            // Sumamos o descontamos el dinero total
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "=====================");
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + "Total: " + total + "$");
            NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_BANK, bank + total);

            // Añadimos el log de pago
            Database.logPayment("Payday", player.Name, "Payday", total);
        }

        public static ItemModel getItemModelFromId(int itemId)
        {
            ItemModel item = null;
            foreach (ItemModel itemModel in itemList)
            {
                if (itemModel.id == itemId)
                {
                    item = itemModel;
                    break;
                }
            }
            return item;
        }

        public static ItemModel getPlayerItemModelFromHash(int playerId, String hash)
        {
            ItemModel itemModel = null;
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_PLAYER && item.ownerIdentifier == playerId && item.hash == hash)
                {
                    itemModel = item;
                    break;
                }
            }
            return itemModel;
        }

        public static ItemModel getClosestItem(Client player)
        {
            ItemModel itemModel = null;
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_GROUND && player.Position.DistanceTo(item.position) < 2.0f)
                {
                    itemModel = item;
                    break;
                }
            }
            return itemModel;
        }

        public static ItemModel getClosestItemWithHash(Client player, String hash)
        {
            ItemModel itemModel = null;
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_GROUND && item.hash == hash && player.Position.DistanceTo(item.position) < 2.0f)
                {
                    itemModel = item;
                    break;
                }
            }
            return itemModel;
        }

        public static ItemModel getItemInEntity(int entityId, String entity)
        {
            ItemModel item = null;
            foreach (ItemModel itemModel in itemList)
            {
                if (itemModel.ownerEntity == entity && itemModel.ownerIdentifier == entityId)
                {
                    item = itemModel;
                    break;
                }
            }
            return item;
        }

        private void substractPlayerItems(ItemModel item, int amount = 1)
        {
            item.amount -= amount;
            if (item.amount == 0)
            {
                // Eliminamos el objeto del jugador
                Database.removeItem(item.id);
                itemList.Remove(item);
            }
        }

        private int getPlayerInventoryTotal(Client player)
        {
            int totalItems = 0;
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_PLAYER && NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID) == item.ownerIdentifier)
                {
                    totalItems++;
                }
            }
            return totalItems;
        }

        private List<InventoryModel> getPlayerInventory(Client player)
        {
            List<InventoryModel> inventory = new List<InventoryModel>();
            int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_PLAYER && item.ownerIdentifier == playerId)
                {
                    BusinessItemModel businessItem = Business.getBusinessItemFromHash(item.hash);
                    if (businessItem != null && businessItem.type != Constants.ITEM_TYPE_WEAPON)
                    {
                        // Creamos el objeto del inventario
                        InventoryModel inventoryItem = new InventoryModel();
                        inventoryItem.id = item.id;
                        inventoryItem.hash = item.hash;
                        inventoryItem.description = businessItem.description;
                        inventoryItem.type = businessItem.type;
                        inventoryItem.amount = item.amount;

                        // Añadimos el objeto al inventario
                        inventory.Add(inventoryItem);
                    }
                }
            }
            return inventory;
        }

        public static List<InventoryModel> getPlayerInventoryAndWeapons(Client player)
        {
            List<InventoryModel> inventory = new List<InventoryModel>();
            int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_PLAYER && item.ownerIdentifier == playerId)
                {
                    BusinessItemModel businessItem = Business.getBusinessItemFromHash(item.hash);
                    if (businessItem != null)
                    {
                        // Creamos el objeto del inventario
                        InventoryModel inventoryItem = new InventoryModel();
                        inventoryItem.id = item.id;
                        inventoryItem.hash = item.hash;
                        inventoryItem.description = businessItem.description;
                        inventoryItem.type = businessItem.type;
                        inventoryItem.amount = item.amount;

                        // Añadimos el objeto al inventario
                        inventory.Add(inventoryItem);
                    }
                }
            }
            return inventory;
        }

        public static List<InventoryModel> getVehicleTrunkInventory(NetHandle vehicle)
        {
            List<InventoryModel> inventory = new List<InventoryModel>();
            int vehicleId = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_ID);
            foreach (ItemModel item in itemList)
            {
                if (item.ownerEntity == Constants.ITEM_ENTITY_VEHICLE && item.ownerIdentifier == vehicleId)
                {
                    // Miramos si es un objeto o un arma
                    InventoryModel inventoryItem = new InventoryModel();
                    BusinessItemModel businessItem = Business.getBusinessItemFromHash(item.hash);

                    if(businessItem != null)
                    {
                        inventoryItem.description = businessItem.description;
                        inventoryItem.type = businessItem.type;
                    }
                    else
                    {
                        inventoryItem.description = item.hash;
                        inventoryItem.type = Constants.ITEM_TYPE_WEAPON;
                    }

                    // Actualizamos el resto de valores
                    inventoryItem.id = item.id;
                    inventoryItem.hash = item.hash;
                    inventoryItem.amount = item.amount;

                    // Añadimos el objeto al inventario
                    inventory.Add(inventoryItem);
                }
            }
            return inventory;
        }

        public static List<ClothesModel> getPlayerClothes(int playerId)
        {
            List<ClothesModel> clothesModelList = new List<ClothesModel>();
            foreach (ClothesModel clothes in clothesList)
            {
                if (clothes.player == playerId)
                {
                    clothesModelList.Add(clothes);
                }
            }
            return clothesModelList;
        }

        public static ClothesModel getDressedClothesInSlot(int playerId, int type, int slot)
        {
            ClothesModel clothesDressed = null;
            foreach (ClothesModel clothes in clothesList)
            {
                if (clothes.player == playerId && clothes.type == type && clothes.slot == slot && clothes.dressed)
                {
                    clothesDressed = clothes;
                    break;
                }
            }
            return clothesDressed;
        }

        public static List<String> getClothesNames(List<ClothesModel> clothesList)
        {
            List<String> clothesNames = new List<String>();
            foreach (ClothesModel clothes in clothesList)
            {
                foreach (BusinessClothesModel businessClothes in Constants.BUSINESS_CLOTHES_LIST)
                {
                    if (businessClothes.clothesId == clothes.drawable && businessClothes.bodyPart == clothes.slot && businessClothes.type == clothes.type)
                    {
                        clothesNames.Add(businessClothes.description);
                        break;
                    }
                }
            }
            return clothesNames;
        }

        public static void undressClothes(int playerId, int type, int slot)
        {
            foreach (ClothesModel clothes in clothesList)
            {
                if (clothes.player == playerId && clothes.type == type && clothes.slot == slot && clothes.dressed)
                {
                    clothes.dressed = false;
                    Database.updateClothes(clothes);
                    break;
                }
            }
        }

        public static void populateCharacterClothes(Client player)
        {
            int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
            foreach (ClothesModel clothes in Globals.clothesList)
            {
                if (clothes.player == playerId && clothes.dressed)
                {
                    if (clothes.type == 0)
                    {
                        NAPI.Player.SetPlayerClothes(player, clothes.slot, clothes.drawable, 0);
                    }
                    else
                    {
                        NAPI.Player.SetPlayerAccessory(player, clothes.slot, clothes.drawable, 0);
                    }
                }
            }
        }

        public static List<TattooModel> getPlayerTattoos(int playerId)
        {
            List<TattooModel> tattooModelList = new List<TattooModel>();
            foreach(TattooModel tattoo in tattooList)
            {
                if(tattoo.player == playerId)
                {
                    tattooModelList.Add(tattoo);
                }
            }
            return tattooModelList;
        }

        private int getPlayerLevel(Client player)
        {
            float playedHours = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PLAYED) / 100;
            return (int)Math.Round(Math.Log(playedHours) * Constants.LEVEL_MULTIPLIER);
        }

        [Command("guardar")]
        public void guardarCommand(Client player)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
            {
                int itemId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                ItemModel item = getItemModelFromId(itemId);
                if (item.objectHandle.IsNull)
                {
                    NAPI.Player.GivePlayerWeapon(player, WeaponHash.Unarmed, 1);
                }
                else
                {
                    NAPI.Entity.DetachEntity(item.objectHandle);
                    NAPI.Entity.DeleteEntity(item.objectHandle);
                }
                item.ownerEntity = Constants.ITEM_ENTITY_PLAYER;
                NAPI.Data.ResetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                Database.updateItem(item);
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_RIGHT_HAND_EMPTY);
            }
        }

        [Command("consumir")]
        public void consumirCommand(Client player)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
            {
                // Obtenemos el objeto de la mano
                int itemId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                ItemModel item = getItemModelFromId(itemId);
                BusinessItemModel businessItem = Business.getBusinessItemFromHash(item.hash);

                // Miramos si es un consumible
                if (businessItem.type == Constants.ITEM_TYPE_CONSUMABLE)
                {
                    String message = String.Format(Messages.INF_PLAYER_INVENTORY_CONSUME, businessItem.description.ToLower());

                    // Consumimos una unidad
                    item.amount--;
                    Database.updateItem(item);

                    // Miramos si cambia la vida
                    if (businessItem.health != 0)
                    {
                        int health = NAPI.Player.GetPlayerHealth(player);
                        NAPI.Player.SetPlayerHealth(player, health + businessItem.health);
                    }

                    // Miramos si sube el nivel de alcohol
                    if (businessItem.alcoholLevel > 0)
                    {
                        float currentAlcohol = 0;
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_DRUNK_LEVEL) == true)
                        {
                            currentAlcohol = NAPI.Data.GetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL);
                        }
                        NAPI.Data.SetEntityData(player, EntityData.PLAYER_DRUNK_LEVEL, currentAlcohol + businessItem.alcoholLevel);

                        // Miramos si ha excedido el nivel de alcohol
                        if (currentAlcohol + businessItem.alcoholLevel > Constants.WASTED_LEVEL)
                        {
                            NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_WALKING_STYLE, "move_m@drunk@verydrunk");
                            NAPI.ClientEvent.TriggerClientEventForAll("changePlayerWalkingStyle", player.Handle, "move_m@drunk@verydrunk");
                        }
                    }

                    // Comprobamos si era el último
                    if (item.amount == 0)
                    {
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                        NAPI.Entity.DetachEntity(item.objectHandle);
                        NAPI.Entity.DeleteEntity(item.objectHandle);
                        Database.removeItem(item.id);
                        itemList.Remove(item);
                    }

                    // Mandamos el mensaje al jugador
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_ITEM_NOT_CONSUMABLE);
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_RIGHT_HAND_EMPTY);
            }
        }

        [Command("inventario")]
        public void inventarioCommand(Client player)
        {
            if (getPlayerInventoryTotal(player) > 0)
            {
                List<InventoryModel> inventory = getPlayerInventory(player);
                NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerInventory", NAPI.Util.ToJson(inventory), Constants.INVENTORY_TARGET_SELF);
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_ITEMS_INVENTORY);
            }
        }

        [Command("comprar")]
        public void comprarCommand(Client player, int amount = 0)
        {
            // Miramos si el jugador está dentro de un negocio
            if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED) > 0)
            {
                int businessId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED);
                BusinessModel business = Business.getBusinessById(businessId);

                // Mostramos el menú en función de la tienda
                switch (business.type)
                {
                    case Constants.BUSINESS_TYPE_CLOTHES:
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_ABOUT_COMPLEMENTS);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_FOR_AVOID_CLIPPING1);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_FOR_AVOID_CLIPPING2);
                        NAPI.ClientEvent.TriggerClientEvent(player, "showClothesBusinessPurchaseMenu");
                        break;
                    case Constants.BUSINESS_TYPE_BARBER_SHOP:
                        NAPI.ClientEvent.TriggerClientEvent(player, "showHairdresserMenu");
                        break;
                    case Constants.BUSINESS_TYPE_TATTOO_SHOP:
                        // Cargamos la lista de tatuajes
                        int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                        List<TattooModel> tattooList = getPlayerTattoos(playerId);
                        NAPI.ClientEvent.TriggerClientEvent(player, "showTattooMenu", NAPI.Util.ToJson(tattooList), business.multiplier);
                        break;
                    default:
                        List<BusinessItemModel> businessItems = Business.getBusinessSoldItems(business.type);
                        NAPI.ClientEvent.TriggerClientEvent(player, "showBusinessPurchaseMenu", NAPI.Util.ToJson(businessItems), business.name, business.multiplier);
                        break;
                }
            }
            else
            {
                // Recorremos la lista de casas
                foreach (HouseModel house in House.houseList)
                {
                    if (player.Position.DistanceTo(house.position) <= 1.5f && player.Dimension == house.dimension)
                    {
                        House.buyHouse(player, house);
                        return;
                    }
                }

                // Recorremos la lista de parkings
                foreach (ParkingModel parking in Parking.parkingList)
                {
                    if (player.Position.DistanceTo(parking.position) < 2.5f && parking.type == Constants.PARKING_TYPE_SCRAPYARD)
                    {
                        if(amount > 0)
                        {
                            int playerMoney = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);
                            if(playerMoney >= amount)
                            {
                                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                                ItemModel item = getPlayerItemModelFromHash(playerId, Constants.ITEM_HASH_BUSINESS_PRODUCTS);

                                if(item == null)
                                {
                                    item = new ItemModel();
                                    item.amount = amount;
                                    item.dimension = 0;
                                    item.position = new Vector3(0.0f, 0.0f, 0.0f);
                                    item.hash = Constants.ITEM_HASH_BUSINESS_PRODUCTS;
                                    item.ownerEntity = Constants.ITEM_ENTITY_PLAYER;
                                    item.ownerIdentifier = playerId;
                                    item.objectHandle = new NetHandle();
                                    item.id = Database.addNewItem(item);
                                    itemList.Add(item);
                                }
                                else
                                {
                                    item.amount += amount;
                                    Database.updateItem(item);
                                }

                                // Restamos el dinero al jugador
                                NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, playerMoney - amount);

                                // Mandamos el mensaje al jugador
                                String message = String.Format(Messages.INF_PRODUCTS_BOUGHT, amount, amount);
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ENOUGH_MONEY);
                            }
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_COMMAND_PURCHASE);
                        }
                        return;
                    }
                }
            }

        }

        [Command("vender", Messages.GEN_SELL_COMMAND, GreedyArg = true)]
        public void venderCommand(Client player, String args) // /vender vehiculo id id/persona precio
        {
            String[] arguments = args.Split(' ');
            int price = 0;
            int targetId = 0;
            int objectId = 0;
            Client target = null;
            String priceString = String.Empty;
            if (arguments.Length > 0)
            {
                switch (arguments[0].ToLower())
                {
                    case "vehiculo":
                        if (arguments.Length > 3)
                        {
                            // Miramos si viene un id o un nombre
                            if (Int32.TryParse(arguments[2], out targetId) == true)
                            {
                                target = getPlayerById(targetId);
                                priceString = arguments[3];
                            }
                            else if (arguments.Length == 5)
                            {
                                target = NAPI.Player.GetPlayerFromName(arguments[2] + " " + arguments[3]);
                                priceString = arguments[4];
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_VEH_COMMAND);
                                return;
                            }

                            // Comprobamos el precio
                            if (Int32.TryParse(priceString, out price) == true)
                            {
                                if (price > 0)
                                {
                                    if (Int32.TryParse(arguments[1], out objectId) == true)
                                    {
                                        NetHandle vehicle = Vehicles.getVehicleById(objectId);

                                        // Miramos si está en un parking
                                        if (vehicle.IsNull)
                                        {
                                            // Miramos si está aparcado
                                            VehicleModel vehModel = Vehicles.getParkedVehicleById(objectId);

                                            if (vehModel != null)
                                            {
                                                if (vehModel.owner == player.Name)
                                                {
                                                    String playerString = String.Format(Messages.INF_VEHICLE_SELL, vehModel.model, target.Name, price);
                                                    String targetString = String.Format(Messages.INF_VEHICLE_SOLD, player.Name, vehModel.model, price);

                                                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_JOB_PARTNER, player);
                                                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_SELLING_PRICE, price);
                                                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_SELLING_HOUSE, objectId);

                                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerString);
                                                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetString);
                                                }
                                                else
                                                {
                                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_VEH_OWNER);
                                                }
                                            }
                                            else
                                            {
                                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_VEHICLE_NOT_EXISTS);
                                            }
                                        }
                                        else
                                        {
                                            foreach (NetHandle veh in NAPI.Pools.GetAllVehicles())
                                            {
                                                if (NAPI.Data.GetEntityData(veh, EntityData.VEHICLE_ID) == objectId)
                                                {
                                                    if (NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_OWNER) == player.Name)
                                                    {
                                                        String vehicleModel = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_MODEL);
                                                        String playerString = String.Format(Messages.INF_VEHICLE_SELL, vehicleModel, target.Name, price);
                                                        String targetString = String.Format(Messages.INF_VEHICLE_SOLD, player.Name, vehicleModel, price);

                                                        NAPI.Data.SetEntityData(target, EntityData.PLAYER_JOB_PARTNER, player);
                                                        NAPI.Data.SetEntityData(target, EntityData.PLAYER_SELLING_PRICE, price);
                                                        NAPI.Data.SetEntityData(target, EntityData.PLAYER_SELLING_VEHICLE, objectId);

                                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerString);
                                                        NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetString);
                                                    }
                                                    else
                                                    {
                                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_VEH_OWNER);
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_VEH_COMMAND);
                                    }
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PRICE_POSITIVE);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_VEH_COMMAND);
                            }
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_VEH_COMMAND);
                        }
                        break;
                    case "casa": // /vender casa id
                        if (arguments.Length < 2)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.GEN_SELL_HOUSE_COMMAND);
                        }
                        else
                        {
                            if (Int32.TryParse(arguments[1], out objectId) == true)
                            {
                                HouseModel house = House.getHouseById(objectId);
                                if (house != null)
                                {
                                    if (house.owner == player.Name)
                                    {
                                        foreach (Client rndPlayer in NAPI.Pools.GetAllPlayers()) // Si hay jugadores dentro no se puede vender.
                                        {
                                            if (NAPI.Data.GetEntityData(rndPlayer, EntityData.PLAYER_PLAYING) == true && NAPI.Data.GetEntityData(rndPlayer, EntityData.PLAYER_HOUSE_ENTERED) == house.id)
                                            {
                                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_OCCUPIED);
                                                return;
                                            }
                                        }
                                        if (arguments.Length == 2)
                                        {
                                            int sellValue = (int)Math.Round(house.price * 0.7);
                                            String playerString = String.Format(Messages.INF_HOUSE_SELL_STATE, sellValue);
                                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_SELLING_HOUSE_STATE, objectId);
                                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerString);
                                        }
                                        else
                                        {
                                            //vender casa id jugador precio

                                            // Miramos si viene un id o un nombre
                                            if (Int32.TryParse(arguments[2], out targetId) == true)
                                            {
                                                target = getPlayerById(targetId);
                                                priceString = arguments[3];
                                            }
                                            else if (arguments.Length == 5)
                                            {
                                                target = NAPI.Player.GetPlayerFromName(arguments[2] + " " + arguments[3]);
                                                priceString = arguments[4];
                                            }
                                            else
                                            {
                                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_HOUSE_COMMAND);
                                                return;
                                            }
                                            //
                                            if (Int32.TryParse(priceString, out price) == true)
                                            {
                                                if (price > 0)
                                                {
                                                    String playerString = String.Format(Messages.INF_HOUSE_SELL, target.Name, price);
                                                    String targetString = String.Format(Messages.INF_HOUSE_SOLD, player.Name, price);

                                                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_JOB_PARTNER, player);
                                                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_SELLING_PRICE, price);
                                                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_SELLING_HOUSE, objectId);

                                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerString);
                                                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetString);
                                                }
                                                else
                                                {
                                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PRICE_POSITIVE);
                                                }
                                            }
                                            else
                                            {
                                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_VEH_COMMAND);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_HOUSE_OWNER);
                                    }
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_NOT_EXISTS);
                                }

                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.GEN_SELL_HOUSE_COMMAND);
                            }
                        }
                        break;
                    case "arma":
                        // Por hacer
                        break;
                    case "pescado":
                        // Miramos si el jugador está dentro de un negocio
                        if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED) > 0)
                        {
                            int businessId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED);
                            BusinessModel business = Business.getBusinessById(businessId);

                            if (business != null && business.type == Constants.BUSINESS_TYPE_FISHING)
                            {
                                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                                ItemModel fishModel = getPlayerItemModelFromHash(playerId, Constants.ITEM_HASH_FISH);

                                if(fishModel == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_FISH_SELLABLE);
                                }
                                else
                                {
                                    // Obtenemos el dinero y las ganancias
                                    int playerMoney = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);
                                    int amount = (int)Math.Round(fishModel.amount * Constants.PRICE_FISH / 1000.0);

                                    // Eliminamos el objeto al jugador
                                    Database.removeItem(fishModel.id);
                                    itemList.Remove(fishModel);

                                    // Le damos el dinero en mano al jugador
                                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, playerMoney + amount);

                                    // Mandamos el mensaje al jugador
                                    String message = String.Format(Messages.INF_FISHING_WON, amount);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_FISHING_BUSINESS);
                            }
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_FISHING_BUSINESS);
                        }
                        break;
                    default:
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_SELL_COMMAND);
                        break;
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.GEN_SELL_COMMAND);
            }
        }

        [Command("ayuda")]
        public void helpCommand(Client player)
        {
            NAPI.ClientEvent.TriggerClientEvent(player, "helptext");
        }

        [Command("bienvenida")]
        public void welcomehelpCommand(Client player)
        {
            NAPI.ClientEvent.TriggerClientEvent(player, "welcomeHelp");
        }

        [Command("mostrar", Messages.GEN_SHOW_DOC_COMMAND)]
        public void mostrarCommand(Client player, String targetString, String documentation)
        {
            if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_KILLED) != 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_IS_DEATH);
            }
            else
            {
                String nameChar = NAPI.Data.GetEntityData(player, "PLAYER_NAME");
                int age = NAPI.Data.GetEntitySharedData(player, "PLAYER_AGE");
                String sexDescription = NAPI.Data.GetEntitySharedData(player, "PLAYER_SEX") == Constants.SEX_MALE ? "Hombre" : "Mujer";
                int currentLicense = 0;
                int targetId = 0;
                Client target = Int32.TryParse(targetString, out targetId) ? getPlayerById(targetId) : NAPI.Player.GetPlayerFromName(targetString);
                switch (documentation.ToLower())
                {
                    case "licencias":
                        String playerLicenses = NAPI.Data.GetEntityData(player, EntityData.PLAYER_LICENSES);
                        String[] playerLicensesArray = playerLicenses.Split(',');
                        NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_CHAT_ME + player.Name + " muestra sus licencias a " + target.Name);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_CHAT_ME + "Has mostrado tus licencias a " + target.Name);
                        foreach (String license in playerLicensesArray)
                        {
                            int currentLicenseStatus = Int32.Parse(license);
                            switch (currentLicense)
                            {
                                case Constants.LICENSE_CAR:
                                    switch (currentLicenseStatus)
                                    {
                                        case -1:
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Turismo: No disponible");
                                            break;
                                        case 0:
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Turismo: Pendiente del examen práctico");
                                            break;
                                        default:
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Turismo: " + currentLicenseStatus + " puntos");
                                            break;
                                    }
                                    break;
                                case Constants.LICENSE_MOTORCYCLE:
                                    switch (currentLicenseStatus)
                                    {
                                        case -1:
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Motocicleta: No disponible");
                                            break;
                                        case 0:
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Motocicleta: Pendiente del examen práctico");
                                            break;
                                        default:
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Motocicleta: " + currentLicenseStatus + " puntos");
                                            break;
                                    }
                                    break;
                                case Constants.LICENSE_TAXI:
                                    if (currentLicenseStatus == -1)
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Taxi: No disponible");
                                    }
                                    else
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_HELP + "Taxi: Vigente");
                                    }
                                    break;
                            }
                            currentLicense++;
                        }
                        break;
                    case "seguro":
                        int playerMedicalInsurance = NAPI.Data.GetEntityData(player, EntityData.PLAYER_MEDICAL_INSURANCE);
                        // Hacemos la conversión del timestamp a fecha
                        System.DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
                        dateTime = dateTime.AddSeconds(playerMedicalInsurance);

                        if (playerMedicalInsurance > 0)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_CHAT_ME + "Has mostrado tu seguro médico a " + target.Name);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_CHAT_ME + player.Name + " muestra el seguro médico a " + target.Name);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Nombre: " + nameChar);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Edad: " + age);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Sexo: " + sexDescription);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Fecha de expiración: " + dateTime.ToShortDateString());
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + "No tienes seguro médico o ya ha caducado.");
                        }

                        break;
                    case "documentacion":
                        int playerDocumentation = NAPI.Data.GetEntityData(player, EntityData.PLAYER_DOCUMENTATION);
                        if (playerDocumentation > 0)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_CHAT_ME + "Has mostrado tu documentación a " + target.Name);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_CHAT_ME + player.Name + " muestra su documentación a " + target.Name);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Nombre: " + nameChar);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Edad: " + age);
                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Sexo: " + sexDescription);
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_UNDOCUMENTED);
                        }
                        break;
                }
            }
        }

        [Command("pagar", Messages.GEN_PAY_COMMAND)]
        public void pagarCommand(Client player, String targetString, int price)
        {
            if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_KILLED) != 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_IS_DEATH);
            }
            else
            {
                int targetId = 0;
                Client target = Int32.TryParse(targetString, out targetId) ? getPlayerById(targetId) : NAPI.Player.GetPlayerFromName(targetString);
                if (target == player)
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOOKER_OFFERED_HIMSELF);
                }
                else
                {
                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_PAYMENT, player);
                    NAPI.Data.SetEntityData(target, EntityData.JOB_OFFER_PRICE, price);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + "Has ofrecido un pago de " + price + "$ a " + target.Name + ".");
                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + player.Name + " te ha ofrecido un pago de " + price + "$. Escribe /aceptar dinero o /cancelar dinero.");
                }
            }
        }

        [Command("ceder", Messages.GEN_GIVE_COMMAND)]
        public void cederCommand(Client player, String targetString)
        {
            if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
            {
                int targetId = 0;
                Client target = Int32.TryParse(targetString, out targetId) ? getPlayerById(targetId) : NAPI.Player.GetPlayerFromName(targetString);

                if(target == null)
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_FOUND);
                }
                else if(player.Position.DistanceTo(target.Position) > 2.0f)
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_TOO_FAR);
                }
                else if(NAPI.Data.HasEntityData(target, EntityData.PLAYER_RIGHT_HAND) == true)
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_TARGET_RIGHT_HAND_NOT_EMPTY);
                }
                else
                {
                    String playerMessage = String.Empty;
                    String targetMessage = String.Empty;

                    int itemId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                    ItemModel item = getItemModelFromId(itemId);

                    // Miramos si es un arma
                    WeaponHash weaponHash = NAPI.Util.WeaponNameToModel(item.hash);

                    if (weaponHash != 0)
                    {
                        NAPI.Player.GivePlayerWeapon(target, weaponHash, 0);
                        NAPI.Player.SetPlayerWeaponAmmo(target, weaponHash, item.amount);
                        NAPI.Player.RemovePlayerWeapon(player, weaponHash);

                        // Creamos los mensajes para los usuarios
                        playerMessage = String.Format(Messages.INF_ITEM_GIVEN, item.hash.ToLower(), target.Name);
                        targetMessage = String.Format(Messages.INF_ITEM_RECEIVED, player.Name, item.hash.ToLower());
                    }
                    else
                    {
                        BusinessItemModel businessItem = Business.getBusinessItemFromHash(item.hash);
                        NAPI.Entity.DetachEntity(item.objectHandle);
                        NAPI.Entity.AttachEntityToEntity(item.objectHandle, target, "PH_R_Hand", businessItem.position, businessItem.rotation);
                        
                        // Creamos los mensajes para los usuarios
                        playerMessage = String.Format(Messages.INF_ITEM_GIVEN, businessItem.description.ToLower(), target.Name);
                        targetMessage = String.Format(Messages.INF_ITEM_RECEIVED, player.Name, businessItem.description.ToLower());
                    }

                    // Cambiamos el objeto de dueño
                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                    NAPI.Data.SetEntityData(target, EntityData.PLAYER_RIGHT_HAND, item.id);
                    item.ownerIdentifier = NAPI.Data.GetEntityData(target, EntityData.PLAYER_SQL_ID);
                    Database.updateItem(item);

                    // Mandamos los mensajes a los jugadores
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerMessage);
                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetMessage);
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_RIGHT_HAND_EMPTY);
            }
        }

        [Command("cancelar", Messages.GEN_GLOBALS_CANCEL_COMMAND)]
        public void cancelarCommand(Client player, String cancel)
        {
            switch (cancel.ToLower())
            {
                case "directo":
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_ON_AIR) == true)
                    {
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_ON_AIR);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_ON_AIR_CANCELED);
                    }
                    break;
                case "servicio":
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_ALREADY_FUCKING) == false)
                    {
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_ALREADY_FUCKING);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                        NAPI.Data.ResetEntityData(player, EntityData.HOOKER_TYPE_SERVICE);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_HOOKER_SERVICE_CANCELED);
                    }
                    break;
                case "dinero":
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PAYMENT) == true)
                    {
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_PAYMENT);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_PAYMENT_CANCELED);
                    }
                    break;
                case "pedidos":
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_DELIVER_ORDER) == true)
                    {
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_DELIVER_ORDER);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_CHECKPOINT);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_VEHICLE);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_WON);

                        // Quitamos los checkpoints
                        NAPI.ClientEvent.TriggerClientEvent(player, "fastFoodDeliverFinished");
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_DELIVERER_ORDER_CANCELED);
                    }
                    break;
                case "pintura":
                    if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_REPAINT_VEHICLE) == true)
                    {
                        // Obtenemos el mecánico y vehículo
                        Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                        NetHandle vehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAINT_VEHICLE);

                        // Obtenemos los antiguos colores
                        int vehicleColorType = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_COLOR_TYPE);
                        String primaryVehicleColor = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_FIRST_COLOR);
                        String secondaryVehicleColor = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_SECOND_COLOR);
                        int vehiclePearlescentColor = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_PEARLESCENT_COLOR);

                        if (vehicleColorType == Constants.VEHICLE_COLOR_TYPE_PREDEFINED)
                        {
                            NAPI.Vehicle.SetVehiclePrimaryColor(vehicle, Int32.Parse(primaryVehicleColor));
                            NAPI.Vehicle.SetVehicleSecondaryColor(vehicle, Int32.Parse(secondaryVehicleColor));
                            NAPI.Vehicle.SetVehiclePearlescentColor(vehicle, vehiclePearlescentColor);
                        }
                        else
                        {
                            String[] primaryColor = primaryVehicleColor.Split(',');
                            String[] secondaryColor = secondaryVehicleColor.Split(',');
                            NAPI.Vehicle.SetVehicleCustomPrimaryColor(vehicle, Int32.Parse(primaryColor[0]), Int32.Parse(primaryColor[1]), Int32.Parse(primaryColor[2]));
                            NAPI.Vehicle.SetVehicleCustomSecondaryColor(vehicle, Int32.Parse(secondaryColor[0]), Int32.Parse(secondaryColor[1]), Int32.Parse(secondaryColor[2]));
                        }

                        // Reseteamos las variables
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_VEHICLE);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_COLOR_TYPE);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_FIRST_COLOR);
                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_SECOND_COLOR);
                        NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRICE);

                        // Cerramos el navegador de repintado
                        NAPI.ClientEvent.TriggerClientEvent(target, "closeRepaintWindow");

                        // Mandamos el mensaje al jugador
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_REPAINT_CANCELED);
                    }
                    break;
                default:
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.GEN_GLOBALS_CANCEL_COMMAND);
                    break;
            }
        }

        [Command("aceptar", Messages.GEN_GLOBALS_ACCEPT_COMMAND)]
        public void acceptCommand(Client player, String accept)
        {
            if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_KILLED) != 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_IS_DEATH);
            }
            else
            {
                switch (accept.ToLower())
                {
                    case "reparacion":
                        if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_REPAIR_VEHICLE) == true)
                        {
                            // Obtenemos el mecánico
                            Client mechanic = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                            
                            if (mechanic != null && mechanic.Position.DistanceTo(player.Position) < 5.0f)
                            {
                                int price = NAPI.Data.GetEntityData(player, EntityData.JOB_OFFER_PRICE);
                                int playerMoney = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);
                                
                                if (playerMoney >= price)
                                {
                                    // Obtenemos el vehículo a reparar y la parte
                                    String type = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAIR_TYPE);
                                    NetHandle vehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAIR_VEHICLE);
                                    
                                    // Obtenemos el dinero y productos del mecánico
                                    int mechanicId = NAPI.Data.GetEntityData(mechanic, EntityData.PLAYER_SQL_ID);
                                    int mechanicMoney = NAPI.Data.GetEntitySharedData(mechanic, EntityData.PLAYER_MONEY);
                                    ItemModel item = getPlayerItemModelFromHash(mechanicId, Constants.ITEM_HASH_BUSINESS_PRODUCTS);
                                    
                                    switch (type.ToLower())
                                    {
                                        case "chasis":
                                            NAPI.Vehicle.RepairVehicle(vehicle);
                                            break;
                                        case "puertas":
                                            for (int i = 0; i < 6; i++)
                                            {
                                                if (NAPI.Vehicle.IsVehicleDoorBroken(vehicle, i) == true)
                                                {
                                                    NAPI.Vehicle.BreakVehicleDoor(vehicle, i, false);
                                                }
                                            }
                                            break;
                                        case "ruedas":
                                            for (int i = 0; i < 4; i++)
                                            {
                                                if (NAPI.Vehicle.IsVehicleTyrePopped(vehicle, i) == true)
                                                {
                                                    NAPI.Vehicle.PopVehicleTyre(vehicle, i, false);
                                                }
                                            }
                                            break;
                                        case "lunas":
                                            for (int i = 0; i < 4; i++)
                                            {
                                                if (NAPI.Vehicle.IsVehicleWindowBroken(vehicle, i) == true)
                                                {
                                                    NAPI.Vehicle.BreakVehicleWindow(vehicle, i, false);
                                                }
                                            }
                                            break;
                                    }
                                    
                                    // Descontamos los productos y pagamos
                                    if(player != mechanic)
                                    {
                                        NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, playerMoney - price);
                                        NAPI.Data.SetEntitySharedData(mechanic, EntityData.PLAYER_MONEY, mechanicMoney + price);
                                    }
                                    item.amount -= NAPI.Data.GetEntityData(player, EntityData.JOB_OFFER_PRODUCTS);
                                    Database.updateItem(item);

                                    // Limpiamos las variables
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAIR_VEHICLE);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAIR_TYPE);
                                    NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRODUCTS);
                                    NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRICE);

                                    // Mandamos el mensaje
                                    String playerMessage = String.Format(Messages.INF_VEHICLE_REPAIRED_BY, mechanic.Name, price);
                                    String mechanicMessage = String.Format(Messages.INF_VEHICLE_REPAIRED_BY, player.Name, price);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerMessage);
                                    NAPI.Chat.SendChatMessageToPlayer(mechanic, Constants.COLOR_INFO + mechanicMessage);

                                    // Guardamos el registro en base de datos
                                    Database.logPayment(player.Name, mechanic.Name, "Reparación", price);
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ENOUGH_MONEY);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_TOO_FAR);
                            }
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_REPAIR_OFFERED);

                        }
                        break;
                    case "pintura":
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_REPAINT_VEHICLE) == true)
                        {
                            // Obtenemos el mecánico
                            Client mechanic = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);

                            if (mechanic != null && mechanic.Position.DistanceTo(player.Position) < 5.0f)
                            {
                                int price = NAPI.Data.GetEntityData(player, EntityData.JOB_OFFER_PRICE);
                                int playerMoney = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);

                                if (playerMoney >= price)
                                {
                                    // Obtenemos el vehículo a repintar y los colores
                                    NetHandle vehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAINT_VEHICLE);
                                    int colorType = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAINT_COLOR_TYPE);
                                    String firstColor = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAINT_FIRST_COLOR);
                                    String secondColor = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAINT_SECOND_COLOR);
                                    int pearlescentColor = NAPI.Data.GetEntityData(player, EntityData.PLAYER_REPAINT_PEARLESCENT);

                                    // Obtenemos el dinero y productos del mecánico
                                    int mechanicId = NAPI.Data.GetEntityData(mechanic, EntityData.PLAYER_SQL_ID);
                                    int mechanicMoney = NAPI.Data.GetEntitySharedData(mechanic, EntityData.PLAYER_MONEY);
                                    ItemModel item = getPlayerItemModelFromHash(mechanicId, Constants.ITEM_HASH_BUSINESS_PRODUCTS);

                                    // Repintamos el vehículo
                                    NAPI.Data.SetEntityData(vehicle, EntityData.VEHICLE_COLOR_TYPE, colorType);
                                    NAPI.Data.SetEntityData(vehicle, EntityData.VEHICLE_FIRST_COLOR, firstColor);
                                    NAPI.Data.SetEntityData(vehicle, EntityData.VEHICLE_SECOND_COLOR, secondColor);
                                    NAPI.Data.SetEntityData(vehicle, EntityData.VEHICLE_PEARLESCENT_COLOR, pearlescentColor);

                                    // Actualizamos el color en base de datos
                                    VehicleModel vehicleModel = new VehicleModel();
                                    vehicleModel.id = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_ID);
                                    vehicleModel.colorType = colorType;
                                    vehicleModel.firstColor = firstColor;
                                    vehicleModel.secondColor = secondColor;
                                    vehicleModel.pearlescent = pearlescentColor;
                                    Database.updateVehicleColor(vehicleModel);

                                    // Descontamos los productos y pagamos
                                    if (player != mechanic)
                                    {
                                        NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, playerMoney - price);
                                        NAPI.Data.SetEntitySharedData(mechanic, EntityData.PLAYER_MONEY, mechanicMoney + price);
                                    }
                                    item.amount -= NAPI.Data.GetEntityData(player, EntityData.JOB_OFFER_PRODUCTS);
                                    Database.updateItem(item);

                                    // Limpiamos las variables
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_VEHICLE);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_COLOR_TYPE);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_FIRST_COLOR);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_REPAINT_SECOND_COLOR);
                                    NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRODUCTS);
                                    NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRICE);

                                    // Mandamos el mensaje
                                    String playerMessage = String.Format(Messages.INF_VEHICLE_REPAINTED_BY, mechanic.Name, price);
                                    String mechanicMessage = String.Format(Messages.INF_VEHICLE_REPAINTED_TO, player.Name, price);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerMessage);
                                    NAPI.Chat.SendChatMessageToPlayer(mechanic, Constants.COLOR_INFO + mechanicMessage);

                                    // Cerramos el navegador de repintado
                                    NAPI.ClientEvent.TriggerClientEvent(mechanic, "closeRepaintWindow");

                                    // Guardamos el registro en base de datos
                                    Database.logPayment(player.Name, mechanic.Name, "Pintura", price);
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ENOUGH_MONEY);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_TOO_FAR);
                            }
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_REPAIR_OFFERED);
                        }
                            
                        break;
                    case "servicio":
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_ALREADY_FUCKING) == true)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_ALREADY_FUCKING);
                        }
                        else if (NAPI.Player.GetPlayerVehicleSeat(player) != Constants.VEHICLE_SEAT_DRIVER)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_VEHICLE_DRIVING);
                        }
                        else
                        {
                            // Obtenemos el vehículo
                            NetHandle vehicle = NAPI.Player.GetPlayerVehicle(player);

                            if (NAPI.Vehicle.GetVehicleEngineStatus(vehicle) == true)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_ENGINE_ON);
                            }
                            else
                            {
                                Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                                if (NAPI.Data.HasEntityData(player, EntityData.HOOKER_TYPE_SERVICE) == true)
                                {
                                    int amount = NAPI.Data.GetEntityData(player, EntityData.JOB_OFFER_PRICE);
                                    int money = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);

                                    if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) == true)
                                    {
                                        if (amount > money)
                                        {
                                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ENOUGH_MONEY);
                                        }
                                        else
                                        {
                                            int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);
                                            int targetMoney = NAPI.Data.GetEntitySharedData(target, EntityData.PLAYER_MONEY);
                                            NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, money - amount);
                                            NAPI.Data.SetEntitySharedData(target, EntityData.PLAYER_MONEY, targetMoney + amount);

                                            // Avisamos del pago
                                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + "Has pagado un servicio de " + amount + "$.");
                                            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "El cliente te ha pagado " + amount + "$ por el servicio.");

                                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_ANIMATION, target);
                                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_ALREADY_FUCKING, target);
                                            NAPI.Data.SetEntityData(target, EntityData.PLAYER_ALREADY_FUCKING, player);

                                            // Reseteamos las variables
                                            NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRICE);
                                            NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);

                                            // Comprobamos el tipo de servicio
                                            if (NAPI.Data.GetEntityData(player, EntityData.HOOKER_TYPE_SERVICE) == Constants.HOOKER_SERVICE_BASIC)
                                            {
                                                NAPI.Player.PlayPlayerAnimation(player, (int)(Constants.AnimationFlags.Loop), "mini@prostitutes@sexlow_veh", "low_car_bj_loop_player");
                                                NAPI.Player.PlayPlayerAnimation(target, (int)(Constants.AnimationFlags.Loop), "mini@prostitutes@sexlow_veh", "low_car_bj_loop_female");

                                                // Creamos el timer para acabar el servicio
                                                Timer sexTimer = new Timer(Hooker.OnSexServiceTimer, player, 120000, Timeout.Infinite);
                                                Hooker.sexTimerList.Add(playerId, sexTimer);
                                            }
                                            else
                                            {
                                                NAPI.Player.PlayPlayerAnimation(player, (int)(Constants.AnimationFlags.Loop), "mini@prostitutes@sexlow_veh", "low_car_sex_loop_player");
                                                NAPI.Player.PlayPlayerAnimation(target, (int)(Constants.AnimationFlags.Loop), "mini@prostitutes@sexlow_veh", "low_car_sex_loop_female");

                                                // Creamos el timer para acabar el servicio
                                                Timer sexTimer = new Timer(Hooker.OnSexServiceTimer, player, 180000, Timeout.Infinite);
                                                Hooker.sexTimerList.Add(playerId, sexTimer);
                                            }

                                            // Añadimos el log del pago
                                            Database.logPayment(player.Name, target.Name, "Servicio de prostitución", amount);
                                        }
                                    }
                                    else
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_FOUND);
                                    }
                                }
                            }
                        }
                        break;

                    // Acaba el aceptar servicio y empieza el aceptar directo.
                    case "directo":
                        if (NAPI.Player.IsPlayerInAnyVehicle(player) == false)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_IN_VEHICLE);
                        }
                        else
                        {
                            NetHandle vehicle = NAPI.Player.GetPlayerVehicle(player);
                            if (NAPI.Player.GetPlayerVehicleSeat(player) != Constants.VEHICLE_SEAT_RIGHT_REAR)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_IN_RIGHT_REAR);
                            }
                            else
                            {
                                Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                                NAPI.Data.SetEntityData(player, EntityData.PLAYER_ON_AIR, true);
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_ALREADY_ON_AIR);
                                NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_SUCCESS + Messages.SUC_INTERVIEW_ACCEPTED);
                            }
                        }
                        break;

                    // Acaba el aceptar directo y empieza el aceptar dinero.
                    case "dinero":
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PAYMENT) == true)
                        {
                            Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_PAYMENT);
                            int amount = NAPI.Data.GetEntityData(player, EntityData.JOB_OFFER_PRICE);

                            if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) == true)
                            {
                                // Calculamos el dinero del pagador
                                int money = NAPI.Data.GetEntitySharedData(target, EntityData.PLAYER_MONEY);

                                if (amount > 0 && money >= amount)
                                {
                                    // Cambiamos el dinero de manos
                                    int playerMoney = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY);
                                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, playerMoney + amount);
                                    NAPI.Data.SetEntitySharedData(target, EntityData.PLAYER_MONEY, money - amount);

                                    // Reseteamos las variables
                                    NAPI.Data.ResetEntityData(player, EntityData.JOB_OFFER_PRICE);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_PAYMENT);

                                    // Enviamos el mensaje
                                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + "Has dado " + amount + "$ a " + player.Name + ".");
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + target.Name + " te ha pagado " + amount + "$.");

                                    // Logeamos el pago en base de datos
                                    Database.logPayment(target.Name, player.Name, "Pago entre jugadores", amount);
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ENOUGH_MONEY);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_FOUND);
                            }
                        }
                        break;

                    // Acepta el vehículo del otro jugador
                    case "vehiculo":
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_SELLING_VEHICLE) == true)
                        {
                            Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                            int amount = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SELLING_PRICE);
                            int vehicleId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SELLING_VEHICLE);

                            if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) == true)
                            {
                                // Calculamos el dinero del pagador
                                int money = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK);

                                if (money >= amount)
                                {

                                    // Obtenemos el vehículo
                                    String vehicleModel = String.Empty;
                                    NetHandle vehicle = Vehicles.getVehicleById(vehicleId);

                                    // Cambiamos el dueño del vehículo
                                    if (vehicle.IsNull)
                                    {
                                        VehicleModel vehModel = Vehicles.getParkedVehicleById(vehicleId);
                                        vehModel.owner = player.Name;
                                        vehicleModel = vehModel.model;
                                    }
                                    else
                                    {
                                        NAPI.Data.SetEntityData(vehicle, EntityData.VEHICLE_OWNER, player.Name);
                                        vehicleModel = NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_MODEL);
                                    }

                                    // Creamos los mensajes de venta
                                    String playerString = String.Format(Messages.INF_VEHICLE_BUY, target.Name, vehicleModel, amount);
                                    String targetString = String.Format(Messages.INF_VEHICLE_BOUGHT, player.Name, vehicleModel, amount);

                                    // Cambiamos el dinero de manos
                                    int targetMoney = NAPI.Data.GetEntitySharedData(target, EntityData.PLAYER_BANK);
                                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_BANK, money - amount);
                                    NAPI.Data.SetEntitySharedData(target, EntityData.PLAYER_BANK, targetMoney + amount);

                                    // Reseteamos las variables
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_SELLING_VEHICLE);
                                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_SELLING_PRICE);

                                    // Enviamos el mensaje
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerString);
                                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetString);

                                    // Logeamos el pago en base de datos
                                    Database.logPayment(target.Name, player.Name, "Venta de vehículo", amount);
                                }
                                else
                                {
                                    String message = String.Format(Messages.ERR_CARSHOP_NO_MONEY, amount);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + message);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_FOUND);
                            }
                        }
                        break;
                    case "casa":
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_SELLING_HOUSE) == true)
                        {
                            Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                            int amount = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SELLING_PRICE);
                            int houseId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SELLING_HOUSE);

                            if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) == true)
                            {
                                // Calculamos el dinero del pagador
                                int money = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK);
                                
                                if (money >= amount)
                                {

                                    // Obtenemos la casa
                                    HouseModel house = House.getHouseById(houseId);
                                    
                                    //SEGURIDAD ADICIONAL: Se podría abusar de vender a la vez la casa al estado y a una persona.
                                    if (house.owner == target.Name)
                                    {
                                        // Cambiamos el dueño de la casa
                                        house.owner = player.Name;
                                        Database.kickTenantsOut(house.id);
                                        house.tenants = 2;
                                        Database.updateHouse(house);

                                        // Creamos los mensajes de venta
                                        String playerString = String.Format(Messages.INF_HOUSE_BUYTO, target.Name, amount);
                                        String targetString = String.Format(Messages.INF_HOUSE_BOUGHT, player.Name, amount);

                                        // Cambiamos el dinero de manos
                                        int targetMoney = NAPI.Data.GetEntitySharedData(target, EntityData.PLAYER_BANK);
                                        NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_BANK, money - amount);
                                        NAPI.Data.SetEntitySharedData(target, EntityData.PLAYER_BANK, targetMoney + amount);

                                        // Reseteamos las variables
                                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_SELLING_HOUSE);
                                        NAPI.Data.ResetEntityData(player, EntityData.PLAYER_SELLING_PRICE);

                                        // Enviamos el mensaje
                                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + playerString);
                                        NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_INFO + targetString);

                                        // Logeamos el pago en base de datos
                                        Database.logPayment(target.Name, player.Name, "Venta de casa", amount);
                                    }
                                    else
                                    {
                                        NAPI.Chat.SendChatMessageToPlayer(player, Messages.ERR_HOUSE_SELL_GENERIC);
                                        NAPI.Chat.SendChatMessageToPlayer(target, Messages.ERR_HOUSE_SELL_GENERIC);
                                    }
                                }
                                else
                                {
                                    String message = String.Format(Messages.ERR_CARSHOP_NO_MONEY, amount);
                                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_ERROR + message);
                                }
                            }
                        }
                        break;
                    case "casaestado":
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_SELLING_HOUSE_STATE) == true)
                        {
                            HouseModel house = House.getHouseById(NAPI.Data.GetEntityData(player, EntityData.PLAYER_SELLING_HOUSE_STATE));
                            int amount = (int)Math.Round(house.price * 0.7);

                            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
                            {

                                //SEGURIDAD ADICIONAL: Que la casa siga siendo mía.
                                if (house.owner == player.Name)
                                {
                                    //Vendemos la casa
                                    house.status = Constants.HOUSE_STATE_BUYABLE;
                                    house.owner = "";
                                    house.locked = true;
                                    NAPI.TextLabel.SetTextLabelText(house.houseLabel, House.getHouseLabelText(house));
                                    NAPI.World.RemoveIpl(house.ipl);
                                    Database.kickTenantsOut(house.id);
                                    house.tenants = 2;
                                    Database.updateHouse(house);

                                    //Metemos el dinero al jugador
                                    int playerMoney = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK);
                                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_BANK, playerMoney + amount);

                                    //Registramos la venta
                                    Database.logPayment(player.Name, "El estado", "Venta de casa", amount);

                                    //Se lo contamos
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_SUCCESS + String.Format(Messages.SUC_HOUSE_SOLD, amount));
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Messages.ERR_HOUSE_SELL_GENERIC);
                                }
                            }
                        }
                        break;
                    default:
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.GEN_GLOBALS_ACCEPT_COMMAND);
                        break;
                }
            }
        }

        [Command("recoger")]
        public void recogerCommand(Client player)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_RIGHT_HAND_OCCUPIED);
            }
            else if(NAPI.Data.HasEntitySharedData(player, EntityData.PLAYER_WEAPON_CRATE) == true)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_BOTH_HAND_OCCUPIED);
            }
            else
            {
                ItemModel item = getClosestItem(player);
                if (item != null)
                {
                    // Obtenemos el objeto del suelo
                    int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);
                    ItemModel playerItem = getPlayerItemModelFromHash(playerId, item.hash);

                    // Borramos el objeto
                    NAPI.Entity.DeleteEntity(item.objectHandle);

                    if (playerItem != null)
                    {
                        NAPI.Entity.DeleteEntity(item.objectHandle);
                        playerItem.amount += item.amount;
                        Database.removeItem(item.id);
                        itemList.Remove(item);
                    }
                    else
                    {
                        playerItem = item;
                    }

                    // Cambiamos los datos del objeto
                    playerItem.ownerEntity = Constants.ITEM_ENTITY_RIGHT_HAND;
                    playerItem.ownerIdentifier = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                    Database.updateItem(playerItem);

                    // Ejecutamos la animación de recoger objetos
                    NAPI.Player.PlayPlayerAnimation(player, 0, "random@domestic", "pickup_low");

                    // Añadimos el objeto a la mano del personaje
                    BusinessItemModel businessItem = Business.getBusinessItemFromHash(playerItem.hash);
                    playerItem.objectHandle = NAPI.Object.CreateObject(UInt32.Parse(playerItem.hash), playerItem.position, new Vector3(0.0f, 0.0f, 0.0f), (byte)playerItem.dimension);
                    NAPI.Entity.AttachEntityToEntity(playerItem.objectHandle, player, "PH_R_Hand", businessItem.position, businessItem.rotation);
                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_RIGHT_HAND, playerItem.id);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_PLAYER_PICKED_ITEM);
                }
                else
                {
                    WeaponCrateModel weaponCrate = Weapons.getClosestWeaponCrate(player);
                    if(weaponCrate != null)
                    {
                        int index = Weapons.weaponCrateList.IndexOf(weaponCrate);
                        weaponCrate.carriedEntity = Constants.ITEM_ENTITY_PLAYER;
                        weaponCrate.carriedIdentifier = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);
                        NAPI.Player.PlayPlayerAnimation(player, (int)(Constants.AnimationFlags.Loop | Constants.AnimationFlags.OnlyAnimateUpperBody | Constants.AnimationFlags.AllowPlayerControl), "anim@heists@box_carry@", "idle");
                        NAPI.Entity.AttachEntityToEntity(weaponCrate.crateObject, player, "PH_R_Hand", new Vector3(0.0f, -0.5f, -0.25f), new Vector3(0.0f, 0.0f, 0.0f));
                        NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_WEAPON_CRATE, index);
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_ITEMS_NEAR);
                    }
                }
            }
        }
        
        [Command("tirar")]
        public void tirarCommand(Client player)
        {
            if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
            {
                int itemId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                ItemModel item = getItemModelFromId(itemId);
                BusinessItemModel businessItem = Business.getBusinessItemFromHash(item.hash);

                // Quitamos una unidad del inventario
                item.amount--;
                Database.updateItem(item);

                // Miramos si hay más objetos en el suelo
                ItemModel closestItem = getClosestItemWithHash(player, item.hash);
                if (closestItem != null)
                {
                    closestItem.amount++;
                    Database.updateItem(item);
                }
                else
                {
                    closestItem = item.Copy();
                    closestItem.amount = 1;
                    closestItem.ownerEntity = Constants.ITEM_ENTITY_GROUND;
                    closestItem.dimension = player.Dimension;
                    closestItem.position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z - 0.8f);
                    closestItem.objectHandle = NAPI.Object.CreateObject(UInt32.Parse(closestItem.hash), closestItem.position, new Vector3(0.0f, 0.0f, 0.0f), (byte)closestItem.dimension);
                    closestItem.id = Database.addNewItem(closestItem);
                    itemList.Add(closestItem);
                }

                // Comprobamos si era el último
                if (item.amount == 0)
                {
                    // Quitamos el objeto de la mano
                    NAPI.Entity.DetachEntity(item.objectHandle);
                    NAPI.Entity.DeleteEntity(item.objectHandle);
                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_RIGHT_HAND);

                    // Actualizamos la lista
                    Database.removeItem(item.id);
                    itemList.Remove(item);
                }

                // Mandamos el mensaje
                String message = String.Format(Messages.INF_PLAYER_INVENTORY_DROP, businessItem.description.ToLower());
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
            }
            else if(NAPI.Data.HasEntitySharedData(player, EntityData.PLAYER_WEAPON_CRATE) == true)
            {
                // Obtenemos el id del personaje
                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);
                WeaponCrateModel weaponCrate = Weapons.getPlayerCarriedWeaponCrate(playerId);

                if (weaponCrate != null)
                {
                    weaponCrate.position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z - 1.0f);
                    weaponCrate.carriedEntity = String.Empty;
                    weaponCrate.carriedIdentifier = 0;

                    // Colocamos el objeto en su posición
                    NAPI.Entity.DetachEntity(weaponCrate.crateObject);
                    NAPI.Entity.SetEntityPosition(weaponCrate.crateObject, weaponCrate.position);

                    // Mandamos el mensaje al jugador
                    String message = String.Format(Messages.INF_PLAYER_INVENTORY_DROP, "caja de armas");
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_RIGHT_HAND_EMPTY);
            }
        }

        [Command("duda", Messages.GEN_HELP_REQUEST, GreedyArg = true)]
        public void dudaCommand(Client player, String message)
        {
            // Obtenemos el id del jugador
            int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);

            // Miramos si tiene alguna duda abierta
            foreach (AdminTicketModel ticket in adminTicketList)
            {
                if (playerId == ticket.playerId)
                {
                    ticket.question = message;
                    return;
                }
            }

            // No tiene ninguna duda, creamos una nueva
            AdminTicketModel adminTicket = new AdminTicketModel();
            adminTicket.playerId = playerId;
            adminTicket.question = message;
            adminTicketList.Add(adminTicket);

            // Enviamos el mensaje al jugador y administradores
            foreach (Client target in NAPI.Pools.GetAllPlayers())
            {
                if (NAPI.Data.HasEntityData(target, EntityData.PLAYER_PLAYING) && NAPI.Data.GetEntityData(target, EntityData.PLAYER_ADMIN_RANK) > 0)
                {
                    NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_ADMIN_INFO + Messages.ADM_NEW_ADMIN_TICKET);
                }
                else if (target == player)
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_SUCCESS + Messages.SUC_HELP_REQUEST_SENT);
                }
            }
        }
        
        [Command("puerta")]
        public void puertaCommand(Client player)
        {
            // Miramos si está en su casa
            foreach (HouseModel house in House.houseList)
            {
                if ((player.Position.DistanceTo(house.position) <= 1.5f && player.Dimension == house.dimension) || NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED) == house.id)
                {
                    if (House.hasPlayerHouseKeys(player, house) == false)
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_HOUSE_OWNER);
                    }
                    else
                    {
                        house.locked = !house.locked;
                        Database.updateHouse(house);

                        // Mandamos el mensaje al jugador
                        NAPI.Chat.SendChatMessageToPlayer(player, house.locked ? Constants.COLOR_INFO + Messages.INF_HOUSE_LOCKED : Constants.COLOR_INFO + Messages.INF_HOUSE_OPENED);
                    }
                    return;
                }
            }

            // Miramos si está en su negocio
            foreach (BusinessModel business in Business.businessList)
            {
                if ((player.Position.DistanceTo(business.position) <= 1.5f && player.Dimension == business.dimension) || NAPI.Data.GetEntityData(player, EntityData.PLAYER_BUSINESS_ENTERED) == business.id)
                {
                    if (Business.hasPlayerBusinessKeys(player, business) == false)
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_BUSINESS_OWNER);
                    }
                    else
                    {
                        business.locked = !business.locked;
                        Database.updateBusiness(business);

                        // Mandamos el mensaje al jugador
                        NAPI.Chat.SendChatMessageToPlayer(player, business.locked ? Constants.COLOR_INFO + Messages.INF_BUSINESS_LOCKED : Constants.COLOR_INFO + Messages.INF_BUSINESS_OPENED);
                    }
                    return;
                }
            }

            // No está en ninguna casa ni negocio
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_HOUSE_BUSINESS);
        }

        [Command("complemento", Messages.GEN_COMPLEMENT_COMMAND)]
        public void complementoCommand(Client player, String type, String action)
        {
            // Obtenemos el identificador del personaje
            int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
            ClothesModel clothes = null;

            if (action.ToLower() == "poner" || action.ToLower() == "quitar")
            {
                switch (type.ToLower())
                {
                    case "mascara":
                        clothes = getDressedClothesInSlot(playerId, 0, Constants.CLOTHES_MASK);
                        if (action.ToLower() == "poner")
                        {
                            if(clothes == null)
                            {
                                clothes = getPlayerClothes(playerId).Where(c => c.slot == Constants.CLOTHES_MASK && c.type == 0).First();
                                if (clothes == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_MASK_BOUGHT);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerClothes(player, clothes.slot, clothes.drawable, clothes.texture);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_MASK_EQUIPED);
                            }
                        }
                        else
                        {
                            if (clothes == null)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_MASK_EQUIPED);
                            }
                            else
                            {
                                NAPI.Player.SetPlayerClothes(player, Constants.CLOTHES_MASK, 0, 0);
                                undressClothes(playerId, 0, Constants.CLOTHES_MASK);
                            }
                        }
                        break;
                    case "bolsa":
                        clothes = getDressedClothesInSlot(playerId, 0, Constants.CLOTHES_BAGS);
                        if (action.ToLower() == "poner")
                        {
                            if (clothes == null)
                            {
                                clothes = getPlayerClothes(playerId).Where(c => c.slot == Constants.CLOTHES_BAGS && c.type == 0).First();
                                if (clothes == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_BAG_BOUGHT);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerClothes(player, clothes.slot, clothes.drawable, clothes.texture);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_BAG_EQUIPED);
                            }
                        }
                        else
                        {
                            if (clothes == null)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_BAG_EQUIPED);
                            }
                            else
                            {
                                NAPI.Player.SetPlayerClothes(player, Constants.CLOTHES_BAGS, 0, 0);
                                undressClothes(playerId, 0, Constants.CLOTHES_BAGS);
                            }
                        }
                        break;
                    case "accesorio":
                        clothes = getDressedClothesInSlot(playerId, 0, Constants.CLOTHES_ACCESSORIES);
                        if (action.ToLower() == "poner")
                        {
                            if (clothes == null)
                            {
                                clothes = getPlayerClothes(playerId).Where(c => c.slot == Constants.CLOTHES_ACCESSORIES && c.type == 0).First();
                                if (clothes == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_ACCESSORY_BOUGHT);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerClothes(player, clothes.slot, clothes.drawable, clothes.texture);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_ACCESSORY_EQUIPED);
                            }
                        }
                        else
                        {
                            if (clothes == null)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_ACCESSORY_EQUIPED);
                            }
                            else
                            {
                                NAPI.Player.SetPlayerClothes(player, Constants.CLOTHES_ACCESSORIES, 0, 0);
                                undressClothes(playerId, 0, Constants.CLOTHES_ACCESSORIES);
                            }
                        }
                        break;
                    case "sombrero":
                        clothes = getDressedClothesInSlot(playerId, 1, Constants.ACCESSORY_HATS);
                        if (action.ToLower() == "poner")
                        {
                            if (clothes == null)
                            {
                                clothes = getPlayerClothes(playerId).Where(c => c.slot == Constants.ACCESSORY_HATS && c.type == 1).First();
                                if (clothes == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_HAT_BOUGHT);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerAccessory(player, clothes.slot, clothes.drawable, clothes.texture);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HAT_EQUIPED);
                            }
                        }
                        else
                        {
                            if (clothes == null)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_HAT_EQUIPED);
                            }
                            else
                            {
                                if(NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_SEX) == Constants.SEX_FEMALE)
                                {
                                    NAPI.Player.SetPlayerAccessory(player, Constants.ACCESSORY_HATS, 57, 0);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerAccessory(player, Constants.ACCESSORY_HATS, 8, 0);
                                }
                                undressClothes(playerId, 1, Constants.ACCESSORY_HATS);
                            }
                        }
                        break;
                    case "gafas":
                        clothes = getDressedClothesInSlot(playerId, 1, Constants.ACCESSORY_GLASSES);
                        if (action.ToLower() == "poner")
                        {
                            if (clothes == null)
                            {
                                clothes = getPlayerClothes(playerId).Where(c => c.slot == Constants.ACCESSORY_GLASSES && c.type == 1).First();
                                if(clothes == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_GLASSES_BOUGHT);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerAccessory(player, clothes.slot, clothes.drawable, clothes.texture);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_GLASSES_EQUIPED);
                            }
                        }
                        else
                        {
                            if (clothes == null)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_GLASSES_EQUIPED);
                            }
                            else
                            {
                                if (NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_SEX) == Constants.SEX_FEMALE)
                                {
                                    NAPI.Player.SetPlayerAccessory(player, Constants.ACCESSORY_GLASSES, 5, 0);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerAccessory(player, Constants.ACCESSORY_GLASSES, 0, 0);
                                }
                                undressClothes(playerId, 1, Constants.ACCESSORY_GLASSES);
                            }
                        }
                        break;
                    case "pendientes":
                        clothes = getDressedClothesInSlot(playerId, 1, Constants.ACCESSORY_EARS);
                        if (action.ToLower() == "poner")
                        {
                            if (clothes == null)
                            {
                                clothes = getPlayerClothes(playerId).Where(c => c.slot == Constants.ACCESSORY_EARS && c.type == 1).First();
                                if (clothes == null)
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_EAR_BOUGHT);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerAccessory(player, clothes.slot, clothes.drawable, clothes.texture);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_EAR_EQUIPED);
                            }
                        }
                        else
                        {
                            if (clothes == null)
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_EAR_EQUIPED);
                            }
                            else
                            {
                                if (NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_SEX) == Constants.SEX_FEMALE)
                                {
                                    NAPI.Player.SetPlayerAccessory(player, Constants.ACCESSORY_EARS, 12, 0);
                                }
                                else
                                {
                                    NAPI.Player.SetPlayerAccessory(player, Constants.ACCESSORY_EARS, 3, 0);
                                }
                                undressClothes(playerId, 1, Constants.ACCESSORY_EARS);
                            }
                        }
                        break;
                    default:
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_COMPLEMENT_COMMAND);
                        break;
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_COMPLEMENT_COMMAND);
            }
        }
    }
}