# API Reference — Top5Agent

Base URL: `http://localhost:{port}/api`
Swagger UI: `http://localhost:{port}/swagger`
Hangfire Dashboard: `http://localhost:{port}/hangfire`

---

## Pipeline

### POST /api/pipeline/run

Starts a pipeline run for a selected niche. Generates video ideas using GPT-4o, deduplicates them, and optionally auto-approves and enqueues script writing.

**Request body**

```json
{
  "niche": 0,
  "count": 10,
  "autoApprove": false
}
```

| Field         | Type    | Required | Default | Description |
|---------------|---------|----------|---------|-------------|
| `niche`       | enum    | yes      | —       | Niche index — see Niche Values below |
| `count`       | integer | no       | `10`    | Number of ideas to generate |
| `autoApprove` | boolean | no       | `false` | If true, approved ideas are immediately sent to script writing without manual review |

**Niche values**

| Value | Niche |
|-------|-------|
| `0`  | Survival |
| `1`  | Danger and Safety |
| `2`  | Travel and Cities |
| `3`  | Animals and Wildlife |
| `4`  | Health and Diseases |
| `5`  | Food and Restaurants |
| `6`  | Technology and Gadgets |
| `7`  | Cars and Mechanics |
| `8`  | Outdoor and Hiking |
| `9`  | Home Improvement |
| `10` | History and Mysteries |
| `11` | Science and Discoveries |
| `12` | Strange Facts |
| `13` | Crime and Prisons |
| `14` | Luxury and Expensive Things |
| `15` | Nature and Disasters |
| `16` | Psychology and Human Behavior |
| `17` | Extreme Places |
| `18` | Life Hacks and Tricks |
| `19` | Everyday Mistakes |

**Response `200 OK`**

```json
{
  "runId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

## Ideas

### GET /api/ideas

Returns all ideas, optionally filtered by status.

**Query parameters**

| Parameter | Type   | Required | Description |
|-----------|--------|----------|-------------|
| `status`  | string | no       | Filter by status: `pending`, `approved`, `rejected`, `scripted` |

**Response `200 OK`**

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "5 Things Mechanics Say You Should Never Do to Your Car",
    "niche": "cars and mechanics",
    "summary": "Common habits drivers think are harmless but actually cause expensive damage.",
    "status": "pending",
    "createdAt": "2024-01-15T09:00:00Z"
  }
]
```

---

### GET /api/ideas/{id}

Returns a single idea by ID.

**Path parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id`      | GUID | Idea ID |

**Response `200 OK`** — full idea object

**Response `404 Not Found`** — idea does not exist

---

### PATCH /api/ideas/{id}/status

Updates the status of an idea. Approving an idea automatically enqueues script writing (ScriptWriter → FactReviewer → ContentPolisher).

**Path parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id`      | GUID | Idea ID |

**Request body**

```json
{
  "status": "approved"
}
```

| Status     | Effect |
|------------|--------|
| `approved` | Enqueues `ProcessIdeaJob` — triggers script writing pipeline |
| `rejected` | Marks idea as rejected, no further processing |

**Response `200 OK`**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "approved"
}
```

**Response `400 Bad Request`** — invalid status value

**Response `404 Not Found`** — idea does not exist

---

## Scripts

### GET /api/scripts/{ideaId}

Returns the script for a given idea, including all sections, reviews, and sources.

**Path parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `ideaId`  | GUID | Idea ID (not script ID) |

**Response `200 OK`**

```json
{
  "id": "...",
  "ideaId": "...",
  "status": "polished",
  "jsonContent": "{ ... full script JSON ... }",
  "rawText": "# Title\n\n## Hook\n...",
  "createdAt": "2024-01-15T09:05:00Z",
  "sections": [
    {
      "id": "...",
      "position": 0,
      "title": "Hook",
      "narration": "...",
      "mediaQuery": null,
      "mediaType": null
    },
    {
      "id": "...",
      "position": 1,
      "title": "Skipping Oil Changes",
      "narration": "...",
      "mediaQuery": "dirty motor oil on dipstick closeup",
      "mediaType": "photo"
    }
  ],
  "reviews": [
    {
      "id": "...",
      "reviewer": "gpt-4o",
      "approved": true,
      "issuesFound": "[]",
      "createdAt": "2024-01-15T09:10:00Z"
    }
  ],
  "sources": [
    {
      "id": "...",
      "url": "https://www.cdc.gov/...",
      "title": "CDC — Engine Oil Facts",
      "verified": false
    }
  ]
}
```

**Section positions**

| Position | Meaning |
|----------|---------|
| `0`      | Hook |
| `1–5`    | Top 5 items |
| `99`     | Outro |

**Script status values**

| Status        | Meaning |
|---------------|---------|
| `draft`       | Script written, not yet fact-checked |
| `reviewed`    | Fact-checked by GPT-4o |
| `needs_review`| Too many unverifiable claims — requires human review |
| `polished`    | Rewritten by Claude for spoken clarity |
| `approved`    | Approved for media download |

**Response `404 Not Found`** — no script exists for this idea

---

### PATCH /api/scripts/{id}/status

Updates the status of a script. Approving a script automatically enqueues media download from Pexels.

**Path parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id`      | GUID | Script ID |

**Request body**

```json
{
  "status": "approved"
}
```

| Status        | Effect |
|---------------|--------|
| `approved`    | Enqueues `DownloadMediaJob` — downloads photos and videos from Pexels |
| `needs_review`| Flags script for manual review, no further processing |

**Response `200 OK`**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "approved"
}
```

**Response `400 Bad Request`** — invalid status value

**Response `404 Not Found`** — script does not exist

---

## Media

### GET /api/media/{scriptId}

Returns all downloaded media assets for a script.

**Path parameters**

| Parameter  | Type | Description |
|------------|------|-------------|
| `scriptId` | GUID | Script ID |

**Response `200 OK`**

```json
[
  {
    "id": "...",
    "scriptSectionId": "...",
    "pexelsId": "12345",
    "assetType": "photo",
    "remoteUrl": "https://images.pexels.com/photos/...",
    "localPath": "media/{runId}/{sectionId}/photo_1.jpg",
    "attribution": "Photo by John Doe on Pexels",
    "createdAt": "2024-01-15T09:20:00Z"
  }
]
```

| Field           | Description |
|-----------------|-------------|
| `assetType`     | `photo` or `video` |
| `localPath`     | Path to downloaded file on the server |
| `attribution`   | Required Pexels attribution string |

---

## End-to-End Workflow

```
1. POST /api/pipeline/run          → generates ideas (status: pending)

2. GET  /api/ideas?status=pending  → review generated ideas

3. PATCH /api/ideas/{id}/status    → approve or reject each idea
                                     approved → triggers script writing automatically

4. GET  /api/scripts/{ideaId}      → review polished script with sections and sources

5. PATCH /api/scripts/{id}/status  → approve script
                                     approved → triggers media download automatically

6. GET  /api/media/{scriptId}      → view downloaded photos and videos
```

---

## Background Jobs (Hangfire)

| Job | Trigger | Retries |
|-----|---------|---------|
| `GenerateIdeasJob` | Daily cron at 09:00 UTC — rotates through 10 niches per day | 3 |
| `ProcessIdeaJob` | Enqueued on idea approval — runs ScriptWriter → FactReviewer → ContentPolisher | 5 |
| `DownloadMediaJob` | Enqueued on script approval — downloads from Pexels per section | 5 |

Monitor jobs at `/hangfire`.
