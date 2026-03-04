# SBAM-Proxy v1.0.0 & Azure Service Bus Local Emulator

[🇺🇸 Read English Version](README.md)

> [!WARNING]
> **SOLO SCOPO DIDATTICO**
> Questo progetto è inteso esclusivamente per scopi di studio e formazione. Non rappresenta una soluzione di livello professionale e non deve essere utilizzato in ambienti di produzione.

## Introduzione
Quante volte, mentre stavi sviluppando in locale per non utilizzare un Service Bus remoto, hai avuto necessità di vedere i messaggi sul tuo emulatore ma non potevi? **Eccoti una soluzione.**

Questo progetto fornisce un ambiente locale completo per lo sviluppo con **Azure Service Bus**. Elimina la necessità di un abbonamento cloud durante lo sviluppo utilizzando l'emulatore ufficiale [Microsoft Azure Service Bus Emulator](https://github.com/Azure/azure-service-bus-emulator-installer) (versione testata: `1.0.0`), potenziato da **SBAM-Proxy** (Service Bus Azure Management Proxy) per supportare strumenti di gestione come [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer) di **Paolo Salvatori**, che ringraziamo per questo incredibile strumento.

### Caratteristiche Principali
- **Emulatore Completo**: Topic, Subscription e Code.
- **Supporto Sessioni**: Supporto nativo per messaggistica con sessioni.
- **SBAM-Proxy**: Un proxy di gestione personalizzato in .NET 8 che simula le API REST di Azure, permettendo a **Service Bus Explorer** di funzionare perfettamente con l'emulatore locale.
- **Conteggi Real-time**: Visualizzazione dei messaggi (Active/DLQ) isolata correttamente per ogni entità.

### Limitazioni Note
A causa dei vincoli architetturali dell'emulatore ASB e della sua dipendenza da un file di configurazione statico:
- **Nessuna Gestione Entità a Runtime**: Non è possibile creare, eliminare o modificare Topic, Subscription o Code tramite Service Bus Explorer o SDK durante l'esecuzione. Tutte le entità devono essere definite in `config.json` prima dell'avvio.
- **Configurazione Statica**: Qualsiasi modifica alla struttura delle entità richiede il riavvio del servizio emulatore.
- **Funzionalità Emulatore**: Sono disponibili solo le funzionalità supportate ufficialmente dall'emulatore Microsoft (es. nessun supporto per Event Hubs o Relays).

## Tecnologie Utilizzate
- **Docker & Docker Compose**: Orchestrazione di SQL Edge, ASB Emulator e Proxy.
- **.NET 8 (C#)**: Logica core di SBAM-Proxy.
- **SQL Server (Edge)**: Backend per la persistenza dell'emulatore.

## Struttura del Progetto
- `sbam-proxy/`: Il proxy di gestione (SBAM-Proxy).
- `config.json`: Definizioni delle entità Service Bus (topic, code).
- `docker-compose.yml`: Orchestrazione della soluzione.

### 1. Configurazione Entità (config.json)
Prima di avviare, definisci i tuoi Topic, Subscription e Code nel file `config.json`.
- **Topic**: Elenca i tuoi topic nell'array `Topics`.
- **Subscription**: Aggiungi le subscription a un topic all'interno del suo array `Subscriptions`.
- **Code**: Definisci code indipendenti nell'array `Queues`.
- **Sessioni**: Imposta `"RequiresSession": true` per abilitare la messaggistica basata su sessioni.

### 2. Prerequisiti
- **Docker Desktop** installato e attivo.
- **Service Bus Explorer** (Client desktop).

### 3. Configurazione Certificato (Windows)
Per permettere a Service Bus Explorer di connettersi in modo sicuro via HTTPS/SSL, è necessario considerare attendibile il certificato locale:
1. Naviga nella cartella `sbam-proxy/`.
2. Esegui `setup_cert.ps1` come Amministratore.
   *Questo creerà un certificato `localhost` e lo aggiungerà alle Autorità di Certificazione Radice Attendibili del tuo PC.*

### 4. Avvio della Soluzione
Dalla root del progetto, esegui:
```powershell
docker compose up -d --build
```
Attendi che `servicebus-emulator` sia in stato `healthy`.

### 5. Connesione con Service Bus Explorer
Usa [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer) di **Paolo Salvatori** con la seguente stringa di connessione:
```
Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```
*Nota: Assicurati di connetterti tramite le porte del proxy (443 per il Management, 5671 per AMQP SSL).*

## Manutenzione e Aggiornamento Configurazione

### Aggiornare le Entità
Se hai bisogno di aggiungere o modificare Topic, Code o Subscription:
1. Modifica il file `config.json`.
2. **Riavvia l'Emulatore**: L'emulatore deve essere riavviato per applicare le modifiche alle entità:
   ```powershell
   docker compose restart servicebus-emulator
   ```
3. **Auto-Reload Proxy**: Il **SBAM-Proxy** rileverà automaticamente i cambiamenti in `config.json` entro 60 secondi (oppure puoi forzare il rebuild con `docker compose up -d --build sbam-proxy`).

### Pulizia
Per fermare e rimuovere tutti i container:
```powershell
docker compose down
```
