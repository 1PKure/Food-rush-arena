# Food Rush Arena

**Food Rush Arena** is a competitive multiplayer prototype developed in Unity using **Photon Fusion 2**.  
The project was created for the subject **Multiplayer Game Programming with Unity**.

## Description

The game allows multiple players to connect to the same match using a **Host-Client** topology.  
Each player controls a character in a competitive scene where they must collect pickups to score points before the match timer ends.

The main goal of the project is to implement a functional real-time networking base using Photon Fusion, including lobby flow, room creation, networked movement, synchronized score logic, and match state management.

## Main Features

- **Photon Fusion 2** integration.
- Lobby system with room creation and joining.
- Multiplayer connection using **Host-Client** topology.
- Networked player movement.
- Synchronized scoring system.
- Networked collectible pickups.
- Match timer.
- Basic gameplay UI.
- Separation between Lobby Scene and Game Scene.

## Technologies Used

- Unity 6
- C#
- Photon Fusion 2
- Git / GitHub

## Controls

| Action | Input |
| Movement | WASD / Arrow Keys |
| Confirm / interact | UI Buttons |
| Exit / return | UI Buttons |

## General Structure

The project is divided into two main sections:

- **Lobby Scene**: allows players to create rooms, search for available sessions, and join a match.
- **Game Scene**: contains the main gameplay logic, including players, pickups, scoring, and match timer.

## Academic Objective

This project was developed as part of a multiplayer programming assignment.  
The objective is to build a competitive multiplayer action game using Photon Fusion 2, with Host-Client connection, synchronized points, and a match time limit.

## Project Status

The project is currently a functional prototype.  
Some visual elements may use placeholders, but the core multiplayer systems are implemented.

## Author

**Matias Pulido**  
Advanced Technical Degree in Video Game Programming with Game Engines
