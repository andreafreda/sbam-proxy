# SBAM-Proxy v1.0.0 & Azure Service Bus Local Emulator

[🇮🇹 Leggi la versione in Italiano](README_it.md)

> [!WARNING]
> **EDUCATIONAL USE ONLY**
> This project is for study and educational purposes only. It does not represent a professional-grade solution and should not be used in production environments.

## Introduction
How many times, while developing locally to avoid using a remote Service Bus, have you needed to see the messages on your emulator but couldn't? **Here is a solution.**

This project provides a complete local environment for **Azure Service Bus** development. It eliminates the need for a cloud subscription during development by using the official [Microsoft Azure Service Bus Emulator](https://github.com/Azure/azure-service-bus-emulator-installer) (tested version: `1.0.0`), enhanced with **SBAM-Proxy** (Service Bus Azure Management Proxy) to support management tools like [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer) by **Paolo Salvatori**, whom we thank for this incredible tool.

### Key Features
- **Full Emulator**: Topics, Subscriptions, and Queues.
- **Session Support**: Native support for sessionful messaging.
- **SBAM-Proxy**: A custom .NET 8 management proxy that mimics the Azure REST API, allowing **Service Bus Explorer** to work seamlessly with the local emulator.
- **Live Counts**: Real-time message counts (Active/DLQ) isolated per entity.

### Known Limitations
Due to the architectural constraints of the underlying ASB Emulator and its reliance on a static configuration file:
- **No Runtime Entity Management**: You cannot create, delete, or modify Topics, Subscriptions, or Queues via Service Bus Explorer or SDKs at runtime. All entities must be defined in `config.json` before startup.
- **Static Configuration**: Any changes to the entity structure require a restart of the emulator service.
- **Emulator Features**: Only features supported by the official Microsoft ASB Emulator are available (e.g., no support for Event Hubs or Relays).

## Technologies
- **Docker & Docker Compose**: Orchestration of SQL Edge, ASB Emulator, and Proxy.
- **.NET 8 (C#)**: SBAM-Proxy core logic.
- **SQL Server (Edge)**: Backend store for the emulator.

## Project Structure
- `sbam-proxy/`: The .NET management proxy (SBAM-Proxy).
- `config.json`: Service Bus entity definitions (topics, queues).
- `docker-compose.yml`: Solution orchestration.

### 1. Entity Configuration (config.json)
Before launching, define your Topics, Subscriptions, and Queues in the `config.json` file.
- **Topics**: List your topics under the `Topics` array.
- **Subscriptions**: Add subscriptions to a topic within its `Subscriptions` array.
- **Queues**: Define standalone queues in the `Queues` array.
- **Sessions**: Set `"RequiresSession": true` to enable session-based messaging.

### 2. Environment Variables (.env)
Create a `.env` file in the root directory (this file is ignored by Git). It must contain:
```env
ACCEPT_EULA=Y
MSSQL_SA_PASSWORD=SbamProxy123!
```
> [!IMPORTANT]
> The password must be at least 8 characters long and contain uppercase, lowercase, numbers, and symbols to meet SQL Server requirements.

### 3. Prerequisites
- **Docker Desktop** installed and running.
- **Service Bus Explorer** (Desktop client).

### 3. Certificate Setup (Windows)
To allow Service Bus Explorer to connect securely via HTTPS/SSL, you must trust the local certificate:
1. Navigate to `sbam-proxy/`.
2. Run `setup_cert.ps1` as Administrator.
   *This creates a local `localhost` certificate and adds it to your Trusted Roots.*

### 4. Launch the Solution
From the project root, run:
```powershell
docker compose up -d --build
```
Wait for `servicebus-emulator` to reach the `healthy` state.

### 5. Connect Service Bus Explorer
Use [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer) by **Paolo Salvatori** with the following connection string:
```
Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```
*Note: Ensure you are connecting via the proxy ports (443 for Management, 5671 for AMQP SSL).*

## Maintenance & Configuration Updates

### Updating Entities
If you need to add or modify Topics, Queues, or Subscriptions:
1. Modify `config.json`.
2. **Restart the Emulator**: The emulator must be restarted to apply entity changes:
   ```powershell
   docker compose restart servicebus-emulator
   ```
3. **Proxy Auto-Reload**: The **SBAM-Proxy** will automatically detect the changes in `config.json` within 60 seconds (or you can force a rebuild with `docker compose up -d --build sbam-proxy`).

### Cleanup
To stop and remove all containers:
```powershell
docker compose down
```
