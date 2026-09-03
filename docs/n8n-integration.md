# Connecting n8n to Silver Task (Phase 62)

A concrete walkthrough for automating Silver Task from [n8n](https://n8n.io/) using an API key —
see [docs/api-keys.md](api-keys.md) for the full authentication architecture this builds on.

## 1. Create a dedicated service account

Use a service account for this integration, not a human's personal API key — see "Security
guidance" below for why.

1. Log in to Silver Task as an Administrator and go to **Admin → API Keys**.
2. Click **New API Key**.
3. Leave "Belongs to" on **+ Create a new service account**, and name it something specific to
   this integration — e.g. `n8n Production`, not just `n8n` (you may have more than one workflow
   or environment later, and each should have its own account/key — see below).
4. Pick a role. **Member** is the right default for most workflows (create/update tasks); use
   **Viewer** for a read-only reporting workflow, or **Manager** only if the workflow needs to
   manage project settings/custom fields.
5. Name the key itself (e.g. `n8n workflow — task sync`) and pick an expiration. 90 days is a
   reasonable default — you'll rotate it periodically (see below), and a scheduled reminder to
   rotate is safer than a key that lives forever unattended.
6. Click **Create Key**. **Copy the key immediately** — it's shown exactly once and can never be
   retrieved again. If you miss it, revoke the key and create a new one; there's no "show again."

## 2. Add the service account to the target project(s)

A freshly created service account isn't a member of any project yet — same as a brand-new human
user. In the project the workflow needs to touch, go to its **Members** section and add the
service account **by the email shown in Admin → API Keys** (an auto-generated address like
`n8n-production-a1b2c3d4@service.invalid` — this is expected; service accounts never have a real
email since they never receive mail or log in).

## 3. Configure n8n

### Credential

In n8n, create a **Header Auth** credential (Credentials → New → Header Auth):

- **Name**: `X-Api-Key`
- **Value**: the key you copied in step 1

Store it only in n8n's credential store — never paste the raw key directly into a node's
parameters, where it would be saved in plain text inside the workflow's own JSON.

### Example: create a task via an HTTP Request node

- **Method**: `POST`
- **URL**: `https://your-instance/api/v1/tasks`
- **Authentication**: Generic Credential Type → Header Auth → the credential above
- **Body** (JSON):
  ```json
  {
    "projectId": "<project id>",
    "title": "{{$json.title}}",
    "description": "{{$json.description}}",
    "priority": "High"
  }
  ```

### Example: list tasks with filtering, for a report workflow

- **Method**: `GET`
- **URL**: `https://your-instance/api/v1/tasks?projectId=<id>&status=InProgress&sort=-dueDate&pageSize=50`

See [docs/public-api.md](public-api.md) for the full pagination/filtering/sorting/search
convention every `/api/v1/*` list endpoint follows.

### A realistic example workflow

"When a form is submitted (n8n Form Trigger) → create a Silver Task task in the intake project
with the form's fields → post a Slack message linking to it." The Silver Task step is exactly the
create-task HTTP Request node above; nothing else in this integration needs to know about API
keys beyond that one node's credential.

## Security guidance

- **One service account per integration**, not one shared account for everything. If a workflow
  is retired or a key leaks, you can revoke exactly that integration's access without touching any
  other automation.
- **Never a human's personal key.** A service account's blast radius is exactly what you added it
  to; a human's own credentials (if this app ever adds self-service personal keys — not built in
  this phase, see docs/api-keys.md) carry that person's own project memberships and role.
- **Rotate periodically.** Admin → API Keys → Rotate immediately revokes the old value and issues
  a new one — update the n8n credential right after.
- **Revoke immediately** if a workflow export/JSON might have been shared or committed somewhere
  with the key pasted directly into a node (rather than referenced via credential) — treat that
  export as compromised the same way you would a leaked password.
- **Scope the role tightly.** A read-only reporting workflow should use a Viewer-role service
  account, not Member or Manager, even though the API itself doesn't force this — the same
  least-privilege principle already applies to human project members.
