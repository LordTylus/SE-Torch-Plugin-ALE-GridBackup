### Introduction
As a servers administrator you probably have to deal with player complaints about grids being broken/deleted due to a bug. 

With the ALE Ownership Logger you can see if you find any evidence about how may have damaged a grid. Or with the ALE Delete Tracker you can see if the missing grid was deleted and probably why. 

But one question remains. If the claims of the player turn out to be true. Do you want to replace his ship or not? And if you do how do you do that? 

There are several options: 

- Ask a player to send you a Blueprint of his ship
 - Problem: What it he doesnt have one? Or worse if he just sends you a Single-Player or Workshop ship instead. Basically using you to spawn stuff in for him he never had. 
- Restore a backup
 - Problem: Other players will be very unhappy if you roll the server back 30 minutes because one grid was damaged or lost.
- Open the Backup on a second server, or Single Player
 - Problem: Singleplayer will have bad simspeed and an other server may just not be available to you. 

So all things have a downside. And even if you manage to get the grid back in, and you have a real blueprint. If build by multiple players you cannot transfer PCU back as you are not able to transfer to multiple people and therefore your block limit settings are not effective anymore. 

And this is where this plugin comes in.

### What does it do?

This plugin can be configured to save all grids you currently have on the server to seperate files. So if one grid goes missing for whatever reason you can restore the last known state of it with a simple command. 

Of course the last known state of the grid may be damaged depending on how fast you can react. But it should be fairly easy to restore a grid that is now gone for quite a while. You have several backups of the same grid, to maximize the likeliness you have a non broken backup. 

Since you can set up the intervals you save the grids yourselves you are able to decide if you want to save all grids every 15 minutes, or every hour, or every 2 hours. A player usually is more happy with having an older version of his ship back, than not having it back at all. 

### Configuration

There are a few configurations to take care of:

- Folder name for Backups
 - What is the folder name inside your Instances Folder where the Grids should be backed up to?
- Save interval in minutes
 - How often do you want all grids of the server saved?
- Number of Backup saves
 - How many versions of the grid do you want to keep? 
- Minimum Block amount for backup
 - Small grids or trash in general may just litter your backups folder. You want to set a minimum amount of blocks a grid needs to have in order to be backuped. 
- Delay (ticks) between two exports
 - To not lag the server the backup exports 1 grid every two ticks. Depending on how much is going on on your server that may be a problematic for simulation speed. so you can increase the delay. Which of course will increase the total time to complete the Backup. 
- Automatically delete backups older than (days):
 - deletes backups of grids that are probably gone from the server for more than X days. default 10. can be disabled by 0 or any negative number. 
- Keep original owner and author
 - If false the grids will be saved ownerless aka nobody. 
- Include connected grids
 - Determines if grids attached via connector will be backuped as well, or if connected grids will be backuped separately. If you backup connected grids as well, they will be saved for the player owning the biggest grid. 
- Include projections
 - Determines if projections blueprints will be backed up as well. Projectors tend to increase the file size immensely so you usually don't want to save whatever they are projecting. 

### Folder Structure

The Backups will be put inside your instances folder. Next to your save file.

It is ordered by Player, Grid and then Date. Multiple players with the same name are unlikely but just for that case the SteamID is added. 

Similary if the player has multiple Grids with the same name the EntityID will be added. 

- Grid Backups
 - &lt;EntityId of Player1&gt;
  - - Miner &lt;EntityId&gt;
  - - - 2019-02-02_12_34_33
  - - - 2019-02-02_13_34_33
  - - - 2019-02-02_14_34_33
 - - Base &lt;EntityId&gt;
  - - - 2019-02-02_12_34_33
  - - - 2019-02-02_13_34_33
  - - - 2019-02-02_14_34_33
 - &lt;EntityId of Player2&gt;
  - - Small Ship 3023 &lt;EntityId&gt;
  - - - 2019-02-02_14_34_33
  - - Static Grid 434 &lt;EntityId&gt;
   - - - 2019-02-02_13_34_33
   - - - 2019-02-02_14_34_33
 - &lt;EntityId of Player3&gt;
  - - Respawn pod &lt;EntityId&gt;
  - - - 2019-02-02_14_34_33

### Commands

- !gridbackup list [PlayerName or SteamID]
 - Lists all grids that are stored for that Player. 
 - If there are multiple players with the same Name you have to input the SteamID instead. 
- !gridbackup list [PlayerName or SteamID] [Gridname or ID]
 - Lists which backups are available for the given Gridname, or ID
 - ID is only necessary if there are multiple grids with the same name for that player.
- !gridbackup restore [PlayerName or SteamID] [Gridname or ID]
 - Restores the latest backup of that the given Grid.
- !gridbackup restore [PlayerName or SteamID] [Gridname or ID] [backup number]
 - Restores a specific backup of that grid. 1 being the latest. 2 being the the one before that etc. 
 - So you may need to try a bit. Which backup you preferably want
- !gridbackup restore [PlayerName or SteamID] [Gridname or ID] [backup number] [keep original position (true/false)]
 - It allows you to paste the grid at the exact same location it once was. However if the location is potentially occupied it will not paste the grid there.
- !gridbackup restore [PlayerName or SteamID] [Gridname or ID] [backup number] [keep original position (true/false)] [force (true/false)]
 - If the Previous command said its potentially blocked by an other grid and you are sure its not (just because something else is too close you can set force to true to paste it anyway. However this may do bad things to your server so be careful.
- !gridbackup save [gridname]
 - Backups the grid you are currently Looking at. Useful for space masters to ensure before doing something with a grid they have a backup of it. 
- !gridbackup run
 - Manually triggers the backup of all grids. 
- !gridbackup clearup &lt;days&gt;
 - Manually deletes backups that are older than the specified amount of days. (Has a confirmation message to prevent accidents)

### Useful Information

A grid may look like **Miner_845215785125785** which is a combination between grid name and entity id. you only need to enter one of these things to find the grid upon restore or List.

Also I implemented Wildcards. So if you use for example M\* or M\*r you can also find the correct folder. Useful for really complicated grid names the players come up with. Wildcards however don't work on IDs. 

### Github
[Find it here](https://github.com/LordTylus/SE-Torch-Plugin-ALE-GridBackup)