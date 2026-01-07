# Revisor Bot — Product Expiration Tracking System

Revisor Bot is a backend-focused Telegram application that extracts product expiration dates from photos of product packaging and proactively notifies users about upcoming expirations.

The system is built as a reliable MVP demonstrating real-world backend engineering practices, including webhook-based integrations, AI-powered structured data extraction, background processing, and safe persistence.

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
- Webhook-based integrations
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



