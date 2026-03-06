# Revisor Bot — Product Expiration Tracking System

Revisor Bot is a backend-focused Telegram application that extracts product expiration dates from photos of product packaging and proactively notifies users about upcoming expirations.

The system is built as a reliable MVP demonstrating real-world backend engineering practices, including webhook-based integrations, AI-powered structured data extraction, background processing, and safe persistence.

## 🎬 Live Demo
<p align="center">
<img src="docs/demo.gif" width="30%" alt="Revisor demo" />
</p>

---

## Problem Statement

Tracking expiration dates of household products is error-prone and inconvenient.  
Users often rely on memory or manual notes, which leads to waste and missed deadlines.

Revisor Bot solves this by allowing users to simply send a photo of a product.  
The system extracts structured metadata, validates it with the user, stores it, and monitors expiration dates automatically.

---

## Key Features

- Photo-based input via Telegram
- AI-powered extraction of product name and expiration date
- Strict structured output using JSON Schema
- User confirmation before data persistence
- PostgreSQL storage with Entity Framework Core
- Weekly background job for expiration checks
- Automatic Telegram notifications
- Product listing and deletion commands


---

## Architecture Overview

---

## Technology Stack

- ASP.NET Core (.NET 8)
- Telegram.Bot
- OpenAI Responses API (Vision + JSON Schema)
- Entity Framework Core
- PostgreSQL (Npgsql provider)
- BackgroundService / PeriodicTimer
- ngrok (local webhook testing)

---
## Getting Started (Local Development)
### Prerequisites

- .NET 8 SDK
- PostgreSQL
- Telegram Bot Token
- OpenAI API Key
- ngrok account (free tier is sufficient)

### Configuration

Create local settings from the template:
```bash
cp Revisor.Bot/appsettings.example.json Revisor.Bot/appsettings.Development.json
```

Then fill in your real secrets (`Telegram:BotToken`, `OpenAI:ApiKey`, database password).

If you want to disable polling (for webhook mode), set:

```
"Telegram": { "UsePolling": false }
```
(or environment variable `Telegram__UsePolling=false`).


#### Database Setup

Apply Entity Framework Core migrations:

```
dotnet ef database update
```

PostgreSQL can be started locally using docker-compose:
```bash
docker compose up -d
```

#### Running the Application

Start the application locally:

```
dotnet run --project Revisor.Bot
```

The service will start on:
```
http://localhost:5023
```

Health check endpoint:
```
GET /health
```
#### Telegram Update Delivery Modes

- **Webhook is supported:** set `Telegram:UsePolling=false` and use ngrok for local end-to-end webhook testing.
- **Current production mode:** we intentionally keep `Telegram:UsePolling=true` (runs `TelegramPollingService`) to avoid extra operational overhead around public webhook endpoint management.

#### Telegram Webhook Setup (Local Development, optional)

Start ngrok:
```
ngrok http 5023
```

Copy the generated HTTPS forwarding URL, for example:
```
https://xxxx.ngrok-free.dev
```

Configure the Telegram webhook:
```
https://api.telegram.org/bot<YOUR_TELEGRAM_BOT_TOKEN>/setWebhook?url=https://xxxx.ngrok-free.dev/telegram/update
```

Verify webhook status:
```
https://api.telegram.org/bot<YOUR_TELEGRAM_BOT_TOKEN>/getWebhookInfo
```
## Core Design Decisions

### Structured AI Output
The OpenAI integration uses enforced JSON Schema to ensure deterministic responses.  
This eliminates fragile text parsing and allows safe downstream processing.

### Confirmation Before Persistence
Extracted data is presented to the user and stored only after explicit confirmation, reducing incorrect or noisy records.

### Safe Date Handling
- Expiration dates are stored as PostgreSQL `date`
- All timestamps use UTC
- Monthly notifications are deduplicated using a `NotifiedForMonth` marker

### Background Processing
Expiration checks are executed by a hosted background service, fully decoupled from request handling.

---
## Web Dashboard (Frontend)

The project includes a web-based dashboard built from scratch to complement the Telegram bot.

The frontend is intentionally minimal and focuses on:
> **Note:** frontend source code is intentionally not included in this public repository at the moment.
> This repository focuses on backend architecture and Telegram/OpenAI integration.

The frontend is intentionally minimal and focuses on:
- Reviewing extracted products
- Searching and deleting items
- Managing notification-related settings
- Visualizing expiration status

### Tech Stack

- React + TypeScript
- Vite
- Fetch API
- No UI framework at the initial stage (progressive enhancement)

### Architecture Principles

- UI components are **pure and stateless**
- All data access goes through a dedicated API layer
- Backend remains the single source of truth
- Frontend mirrors backend domain concepts

### Local Development

```
npm install
npm run dev
```

---

## Data Model Overview

- **User**
  - TelegramChatId (unique)
  - CreatedAtUtc

- **Product**
  - ProductName
  - ExpiryDate
  - Confidence score
  - Notes
  - CreatedAtUtc
  - NotifiedForMonth
  - Foreign key to User

---

## Typical Flow

1. User sends a photo of a product
2. The system extracts structured metadata using OpenAI Vision
3. The user confirms or discards the detected data
4. Confirmed products are stored in PostgreSQL
5. A weekly background job checks for upcoming expirations
6. The user receives a notification if action is required

---

## What This Project Demonstrates

- Backend system design and architecture
- Telegram integrations (polling in production, webhook for local testing)
- AI-assisted automation with deterministic outputs
- Asynchronous and background processing
- Relational data modeling and migrations
- Production-safe date and time handling
- Clean separation of concerns

---

## Possible Extensions

- Image preprocessing and compression
- OCR fallback pipeline
- Product categorization and tagging
- Web dashboard
- Cloud deployment
- Multi-language support

---

## Author

This project was developed as a portfolio application to demonstrate modern backend engineering practices and AI-assisted workflows.

## Usage / Permissions
This repository is provided for viewing and evaluation purposes only.
No license is granted for use, copying, modification, or distribution.


