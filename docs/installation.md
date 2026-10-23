# Installing Buddy

This guide is for anyone who wants to run their own Buddy, for one family or a few. It covers what
you need, how to get Buddy running, and every setting and command-line argument you can pass to
it. Once Buddy is running, the first guardian who signs in gets a guided setup in the app, so this
guide stops there.

- [What you are installing](#what-you-are-installing)
- [Choose how to install](#choose-how-to-install)
- [Install on your own server](#install-on-your-own-server)
- [Create the first guardian](#create-the-first-guardian)
- [Settings reference](#settings-reference)
- [Command reference](#command-reference)
- [After installing](#after-installing)

## What you are installing

Buddy is five containers:

| Container  | What it does                                                                         |
| ---------- | ------------------------------------------------------------------------------------ |
| `caddy`    | The only public part. Serves HTTPS on ports 80 and 443 and gets Let's Encrypt certs  |
| `frontend` | The Angular app, as static files                                                     |
| `api`      | The .NET API                                                                         |
| `keycloak` | Sign-in, accounts and passwords                                                      |
| `db`       | PostgreSQL 18, with two databases: Buddy's own and Keycloak's (both in one instance) |

Each gets its own domain: `app.` for the app, `api.` for the API and `auth.` for Keycloak.

## Choose how to install

| Way                                                                | Pick it when                                                               | Command             |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------- | ------------------- |
| [Published images on your own server](#install-on-your-own-server) | You want a release as it is. Recommended                                   | `task deploy:pull`  |
| [Build on your own server](#install-on-your-own-server)            | You run a fork, or changes that aren't released yet                        | `task deploy`       |
| [Azure Container Apps](../deploy/README-azure.md)                  | You want Azure to run the containers and the database for you (costs more) | `task deploy:azure` |
| [Development container](../.devcontainer/README.md)                | You want to work on Buddy's code, not run it for a family                  | See its guide       |

The rest of this guide covers your own server. The Azure path has its own guide,
[deploy/README-azure.md](../deploy/README-azure.md); its settings are listed
[below](#azure-deployazureenv).

## Install on your own server

This is the short version. [deploy/README.md](../deploy/README.md) has each step in full, with
the background and what to do when something fails.

### What you need

- A Linux server with a public IP address, 2 CPUs and 4 GB of memory or more. Both `amd64` and
  `arm64` work, so Oracle Cloud's Always Free ARM VM (`VM.Standard.A1.Flex`) is enough.
- Docker with the compose plugin, `git` and [Task](https://taskfile.dev) on the server.
- A domain where you can add three DNS records.
- An SMTP account to send email from (invitations and email verification), for example your mail
  provider's or a relay such as Brevo.
- Only ports 22, 80 and 443 open to the internet.

### Steps

1. **Point DNS at the server.** Create A records for `api.yourdomain.com`, `auth.yourdomain.com`
   and `app.yourdomain.com` that point at the server's public IP. Caddy can only get certificates
   once these resolve.

2. **Get the code for the release.** On the server:

   ```bash
   git clone https://github.com/kimipsen/buddy.git && cd buddy
   git checkout v1.6.1   # the release you want; see the GitHub releases page
   ```

   The images come from the registry, but the compose file, the Caddyfile and the scripts come
   from this checkout, so check out the same release you deploy.

3. **Fill in the settings.**

   ```bash
   cp deploy/.env.example deploy/.env
   ```

   Set at least `DEPLOY_HOST` (the output of `hostname`), the three domains, `POSTGRES_PASSWORD`,
   `KEYCLOAK_ADMIN_PASSWORD` and the `Mail__*` settings. `KEYCLOAK_ADMIN_CLI_SECRET` can stay a
   placeholder for now. Every setting is described in [`deploy/.env`](#your-own-server-deployenv).

4. **Start Buddy.**

   ```bash
   task deploy:pull
   ```

   This checks the settings (see [preflight](#preflight-checks)), pulls the published images for
   the release HEAD is on, starts all five containers and waits up to 10 minutes for them to be
   healthy. Use `task deploy` instead to build the images on the server.

5. **Set up the Keycloak realm.** Sign in at `https://auth.yourdomain.com` as `KEYCLOAK_ADMIN` and
   create the `buddy` realm. The quickest way is to import
   [`.devcontainer/keycloak/buddy-realm.json`](../.devcontainer/keycloak/buddy-realm.json) (**Create
   realm > Resource file**) and then:
   - delete the development users `alice`, `bob` and `carol` (**Users**), which have known
     passwords;
   - set `buddy-frontend`'s **Valid redirect URIs** to `https://app.yourdomain.com/*` and its
     **Web origins** to `https://app.yourdomain.com`;
   - set the realm's SMTP server (**Realm settings > Email**), so Keycloak can send password-reset
     emails;
   - generate a new secret for `buddy-admin-cli` (**Clients > buddy-admin-cli > Credentials**),
     put it in `deploy/.env` as `KEYCLOAK_ADMIN_CLI_SECRET`, and restart the API:

     ```bash
     cd deploy && docker compose -f docker-compose.prod.yml up -d --no-build api
     ```

   Step 5 of [deploy/README.md](../deploy/README.md#5-configure-the-realm) explains what the realm
   needs if you create it by hand instead.

6. **Check it works.**

   ```bash
   curl -fsS https://api.yourdomain.com/health    # Healthy
   curl -fsS https://api.yourdomain.com/version   # {"version":"1.6.1","commit":"..."}
   curl -fsS https://app.yourdomain.com/config/runtime-config.json
   ```

   More checks are in step 6 of [deploy/README.md](../deploy/README.md#6-verify-the-deploy).

## Create the first guardian

The realm doesn't let people register themselves, so you create the first guardian in Keycloak:

1. **Users > Create new user.** Fill in username, email, first and last name, and turn on **Email
   verified** (an invitation can only be accepted by an account with a verified email).
2. **Credentials > Set password**, with **Temporary** on if someone else will sign in.
3. **Groups > Join group > `buddy-guardian`.** Without it, Buddy turns the account away with
   `403 not_a_buddy_member`.

That guardian signs in at `https://app.yourdomain.com` and the in-app guided setup takes over: a
group, the children (Buddy creates their accounts in Keycloak and puts them in `buddy-child`),
invitations to other guardians, a calendar, a routine and a meal plan. Other guardians are invited
from the app, but each of them needs a Keycloak account first, made the same way, including the
`buddy-guardian` group: Buddy never adds a guardian to that group itself.

## Settings reference

### How settings reach Buddy

- **Your own server:** everything goes in `deploy/.env`. Docker compose reads it, and the `api`,
  `db`, `keycloak` and `caddy` containers get every line in it as environment variables. After a
  change, run `task deploy:pull` again, or restart just the container that reads it (for example
  `docker compose -f docker-compose.prod.yml up -d --no-build api`).
- **Azure:** `deploy/azure/.env` is read by `deploy.sh`, which sets the containers' environment
  variables. Rerun `task deploy:azure` after a change.
- **The API** is an ASP.NET Core app, so each setting can be given as an environment variable
  with `__` (two underscores) between the levels: `Mail:Host` becomes `Mail__Host`, and the first
  item of the list `Cors:AllowedOrigins` becomes `Cors__AllowedOrigins__0`. Names are not case
  sensitive. Durations are written `hh:mm:ss` (`00:05:00` is five minutes) or `d.hh:mm:ss`.

The API checks its settings when it starts. A missing required setting, a value out of range or a
misspelt feature flag stops it, and its log (`docker compose logs api`) names the setting.

### Your own server: `deploy/.env`

| Variable                      | Required | What it does                                                                                                                                                |
| ----------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DEPLOY_HOST`                 | Yes      | The server's `hostname`. `task deploy` and `task deploy:pull` refuse to run on any other machine                                                            |
| `API_DOMAIN`                  | Yes      | The API's domain, e.g. `api.yourdomain.com`                                                                                                                 |
| `AUTH_DOMAIN`                 | Yes      | Keycloak's domain. Tokens must be issued by exactly `https://<AUTH_DOMAIN>/realms/buddy`                                                                    |
| `APP_DOMAIN`                  | Yes      | The app's domain. Also the only origin the API allows (CORS) and the base of the links in emails                                                            |
| `POSTGRES_USER`               | Yes      | The PostgreSQL user, used by both the API and Keycloak. Default `postgres`                                                                                  |
| `POSTGRES_PASSWORD`           | Yes      | Its password. The deploy refuses the placeholder `change-me`                                                                                                |
| `POSTGRES_DB`                 | Yes      | Buddy's database name. Default `buddy`. Keycloak's database is always `keycloak`                                                                            |
| `KEYCLOAK_ADMIN`              | Yes      | The Keycloak admin console user, created on the first boot only                                                                                             |
| `KEYCLOAK_ADMIN_PASSWORD`     | Yes      | Its password, read on the first boot only. The deploy refuses the placeholder                                                                               |
| `KEYCLOAK_ADMIN_CLI_SECRET`   | Yes      | The `buddy-admin-cli` client secret, which the API uses to create child accounts. A placeholder only warns, so the first boot works before the realm exists |
| `Mail__*`                     | Yes      | The API's SMTP server, see [Email](#email). Without it no invitation or verification email goes out                                                         |
| `BUDDY_IMAGE_REGISTRY`        | No       | Where `task deploy:pull` gets the images. Blank means `ghcr.io/kimipsen`; a fork that publishes its own sets `ghcr.io/<owner>`                              |
| `REPOSITORY_URL`              | No       | The "GitHub" link in the profile menu. Blank means `https://github.com/kimipsen/buddy`                                                                      |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | No       | An OpenTelemetry endpoint for traces, metrics and logs. Blank sends none, see [observability](backend/observability.md)                                     |
| `Features__<Name>`            | No       | Turns a feature off, see [Feature flags](#feature-flags)                                                                                                    |

`deploy/.env` can hold any other API setting from the tables below too, such as
`RateLimiting__*`, `Mealie__*` or `AiAssistant__*`. The exceptions are the settings that
[`docker-compose.prod.yml`](../deploy/docker-compose.prod.yml) sets itself from the variables
above (the connection string, the `Authentication__*` settings, `Cors__AllowedOrigins__0`,
`Mail__FrontendBaseUrl` and `ForwardedHeaders__KnownNetworks__*`). A line for those in `.env` has
no effect; change the compose file instead.

### Feature flags

Every feature is on by default. Turn off one this family won't use with `Features__<Name>=false`
in `deploy/.env` (on Azure: `FEATURES=Medicines=false,SleepDiary=false`) and restart the API. The
API stops serving the feature and the app hides its pages, cards and links, without a rebuild. The
feature's data is kept, and comes back when you set the flag to `true` again. Turning off health
features is also the first step of [setting up privacy](privacy-setup.md#2-turn-off-what-you-dont-use).

| Name                  | Turns off                                                                                  |
| --------------------- | ------------------------------------------------------------------------------------------ |
| `Mealplans`           | Meals and meal plans, and with them the three below                                        |
| `MealplanAiAssistant` | The AI assistant that drafts meal plans, and with it everything sent to an AI provider     |
| `MealplanImport`      | Importing old meal plans from notes and CSV files                                          |
| `MealplanMealie`      | The Mealie integration                                                                     |
| `Medicines`           | Medicine schedules, doses and effect notes (health data)                                   |
| `SleepDiary`          | The sleep diary and its share link (health data)                                           |
| `Feelings`            | Feelings check-ins and their share link                                                    |
| `VisitSummaries`      | Doctor visit summaries. Also off when `Medicines`, `SleepDiary` and `Feelings` are all off |
| `HouseRules`          | House rules                                                                                |
| `Pickups`             | Pickup and drop-off planning                                                               |
| `Babysitters`         | Saved babysitters                                                                          |
| `WorkLocations`       | Guardians' work locations                                                                  |
| `Printing`            | Printable week plans and print templates                                                   |
| `Progress`            | Goal posts, stars and rewards                                                              |
| `TaskLibrary`         | The reusable task library                                                                  |
| `TransitionWarnings`  | Transition warnings on the child's screen                                                  |
| `FirstThen`           | First–Then boards                                                                          |
| `Homework`            | The homework splitter                                                                      |
| `Help`                | The in-app help panel and help pages                                                       |

[feature-flags.md](backend/analysis/feature-flags.md) has what each flag hides in detail.

### Email

The API sends group and guardian invitations and email-verification links. Keycloak sends its own
emails (password reset) through the SMTP server in **Realm settings > Email**, so set both.

| Setting                       | Default                | What it does                                                                                     |
| ----------------------------- | ---------------------- | ------------------------------------------------------------------------------------------------ |
| `Mail__Host`                  | `mailpit`              | The SMTP server. The default is the development mail catcher, which isn't on a production server |
| `Mail__Port`                  | `1025`                 | Its port, usually `587` or `465`                                                                 |
| `Mail__UseSsl`                | `false`                | `true` for implicit TLS (port 465). `false` uses STARTTLS when the server offers it (port 587)   |
| `Mail__Credentials__Username` | none                   | The SMTP login. Leave both credentials out for a server that needs none                          |
| `Mail__Credentials__Password` | none                   | The SMTP password. A username without a password stops the API from starting                     |
| `Mail__FromAddress`           | `no-reply@buddy.local` | The sender. Most providers only accept an address or domain you've verified with them            |
| `Mail__FromName`              | none                   | The sender's name, e.g. `Buddy`                                                                  |
| `Mail__FrontendBaseUrl`       | `https://<APP_DOMAIN>` | The start of the links in emails. Set by the compose file and `deploy.sh`                        |

### API settings

These are set for you by the compose file or `deploy.sh`. You only need them to run the API some
other way, such as on Kubernetes or a host without Docker.

| Setting                                          | What it does                                                                                                                        |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings__Postgres`                    | The Npgsql connection string to Buddy's database. Add `;Maximum Pool Size=N` to change the pool size (default 50)                   |
| `Authentication__Keycloak__Authority`            | Where the API fetches Keycloak's signing keys, e.g. `http://keycloak:8080/realms/buddy`                                             |
| `Authentication__Keycloak__ValidIssuer`          | The issuer tokens must have, e.g. `https://auth.yourdomain.com/realms/buddy`. Defaults to the authority                             |
| `Authentication__Keycloak__Audience`             | The audience tokens must have, `buddy-api`. Empty turns the audience check off                                                      |
| `Authentication__Keycloak__RequireHttpsMetadata` | Whether the authority must be HTTPS. Default `true`; `false` when it's an internal `http://` address                                |
| `Authentication__KeycloakAdmin__Realm`           | The realm the API creates child accounts in. Default `buddy`                                                                        |
| `Authentication__KeycloakAdmin__TokenEndpoint`   | The realm's token endpoint, for the `buddy-admin-cli` service account                                                               |
| `Authentication__KeycloakAdmin__AdminBaseUrl`    | The realm's admin API, e.g. `http://keycloak:8080/admin/realms/buddy`                                                               |
| `Authentication__KeycloakAdmin__ClientId`        | Default `buddy-admin-cli`                                                                                                           |
| `Authentication__KeycloakAdmin__ClientSecret`    | That client's secret (`KEYCLOAK_ADMIN_CLI_SECRET`)                                                                                  |
| `Cors__AllowedOrigins__0`, `__1`, …              | The origins the browser may call the API from: the app's URL                                                                        |
| `ForwardedHeaders__KnownNetworks__0`, `__1`, …   | Networks (CIDR) whose `X-Forwarded-For` the API trusts, i.e. your reverse proxy's. Rate limiting of anonymous callers depends on it |
| `ASPNETCORE_ENVIRONMENT`                         | `Production`                                                                                                                        |
| `ASPNETCORE_URLS`                                | Where the API listens. The image sets `http://+:8080`                                                                               |
| `Logging__LogLevel__Default`                     | Log level, default `Information`. Also per category, e.g. `Logging__LogLevel__Microsoft.AspNetCore=Warning`                         |

### Optional API settings

| Setting                        | Default            | What it does                                                                                               |
| ------------------------------ | ------------------ | ---------------------------------------------------------------------------------------------------------- |
| `AiAssistant__OpenAiModel`     | `gpt-4.1`          | The model the meal-plan assistant uses for a family that picked OpenAI                                     |
| `AiAssistant__GeminiModel`     | `gemini-2.5-flash` | The same for Google Gemini                                                                                 |
| `AiAssistant__AnthropicModel`  | `claude-sonnet-5`  | The same for Anthropic                                                                                     |
| `Mealie__AllowPrivateNetworks` | `false`            | `true` lets families connect a Mealie on a private address, such as one on the same server or home network |
| `Mealie__AllowHttp`            | `false`            | `true` allows a Mealie without HTTPS. Only for a Mealie on your own network                                |
| `Mealie__AllowedHosts__0`, …   | any                | Only these Mealie host names. Empty allows any host that passes the address check                          |
| `Mealie__TimeoutSeconds`       | `10`               | How long to wait for Mealie, 1 to 120                                                                      |
| `Mealie__MaxResponseBytes`     | `2097152` (2 MB)   | The largest Mealie response accepted                                                                       |
| `OTEL_EXPORTER_OTLP_ENDPOINT`  | none               | An OpenTelemetry endpoint, see [observability](backend/observability.md)                                   |

There is nothing to set for the AI providers themselves: each family enters its own API key in
the app.

### Rate limits

Every endpoint is rate limited. The defaults suit a family or two; raise them if many families
share one installation behind one IP address. Set one number and the rest keep their defaults,
e.g. `RateLimiting__Anonymous__TokenLimit=120`. The reasoning behind each is in
[rate-limiting.md](backend/analysis/rate-limiting.md).

Token buckets take `TokenLimit`, `TokensPerPeriod` and `ReplenishmentPeriod`:

| Setting                       | Per            | Default                       |
| ----------------------------- | -------------- | ----------------------------- |
| `RateLimiting__Authenticated` | Signed-in user | 200 requests, 5 more a second |
| `RateLimiting__Anonymous`     | IP address     | 60 requests, 1 more a second  |
| `RateLimiting__IcalFeed`      | iCal feed link | 10 requests, 1 more per 5 min |

Fixed windows take `PermitLimit` and `Window`:

| Setting                            | Per                                | Default           |
| ---------------------------------- | ---------------------------------- | ----------------- |
| `RateLimiting__AiAssistant`        | User, on the AI assistant          | 20 per minute     |
| `RateLimiting__OutboundEmail`      | User, on endpoints that send email | 20 per hour       |
| `RateLimiting__PersonalDataExport` | User, on the data export           | 1 per 10 minutes  |
| `RateLimiting__VisitSummaryShare`  | Doctor visit summary share link    | 30 per 10 minutes |
| `RateLimiting__Mealie`             | User, on the Mealie endpoints      | 60 per minute     |
| `RateLimiting__MealieImages`       | User, on Mealie recipe photos      | 120 per minute    |

For example `RateLimiting__OutboundEmail__Window=00:30:00`.

### Frontend container

The frontend image reads three environment variables when it starts, so one image works for any
installation. The compose file and `deploy.sh` set them.

| Variable             | What it does                                                                       |
| -------------------- | ---------------------------------------------------------------------------------- |
| `API_BASE_URL`       | The API's URL, e.g. `https://api.yourdomain.com`. The app won't start without it   |
| `KEYCLOAK_AUTHORITY` | Keycloak's URL, e.g. `https://auth.yourdomain.com`. The app won't start without it |
| `REPOSITORY_URL`     | The "GitHub" link in the profile menu. Optional                                    |

It serves them as `/config/runtime-config.json` and allows only those two addresses in its
Content-Security-Policy. The version shown in the app is baked in when the image is built
(`BUDDY_VERSION` build argument).

### Azure: `deploy/azure/.env`

[`deploy/azure/.env.example`](../deploy/azure/.env.example) describes each one, and
[README-azure.md](../deploy/README-azure.md) the steps.

| Variable                                                                    | What it does                                                                   |
| --------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `RESOURCE_GROUP`, `LOCATION`, `CONTAINERAPPS_ENV`                           | Where the resources are created                                                |
| `ACR_NAME`, `PG_SERVER_NAME`                                                | The container registry and PostgreSQL server; must be unique across Azure      |
| `PG_ADMIN_USER`, `PG_ADMIN_PASSWORD`, `APP_DB_NAME`                         | The database                                                                   |
| `KEYCLOAK_ADMIN`, `KEYCLOAK_ADMIN_PASSWORD`, `KEYCLOAK_ADMIN_CLI_SECRET`    | As on your own server                                                          |
| `BREVO_SMTP_LOGIN`, `BREVO_SMTP_KEY`, `MAIL_FROM_ADDRESS`, `MAIL_FROM_NAME` | Email through Brevo, for both the API and Keycloak. No email until all are set |
| `FRONTEND_CUSTOM_DOMAIN`, `API_CUSTOM_DOMAIN`, `KEYCLOAK_CUSTOM_DOMAIN`     | Your own domains. Blank uses the `*.azurecontainerapps.io` ones                |
| `BUDDY_IMAGE_VERSION`, `BUDDY_IMAGE_REGISTRY`                               | Deploy a published release (`1.6.1`) instead of building. Blank builds         |
| `REPOSITORY_URL`                                                            | As on your own server                                                          |
| `FEATURES`                                                                  | Features to turn off, e.g. `Medicines=false,SleepDiary=false`                  |

## Command reference

Run the `task` commands from the root of the checkout.

### Deploying

| Command                          | What it does                                                                                    |
| -------------------------------- | ----------------------------------------------------------------------------------------------- |
| `task deploy:pull`               | Runs the published images of the release HEAD is checked out on                                 |
| `task deploy:pull VERSION=1.6.1` | Runs that release. Warns when HEAD isn't on `v1.6.1`. Same as `task deploy:pull -- 1.6.1`       |
| `task deploy:pull VERSION=edge`  | Runs the newest `master` build. `VERSION=sha-<12-char commit>` runs one particular build        |
| `task deploy`                    | Builds the images from the checkout on the server and runs them, tagged with the commit         |
| `task deploy:azure`              | Deploys to Azure Container Apps with the settings in `deploy/azure/.env`                        |
| `task deploy:migrate-from-azure` | Copies the data of an Azure installation onto this server, leaving Azure running. Options below |

`task deploy:migrate-from-azure -- <options>` passes these to
[`migrate-from-azure.sh`](../deploy/migrate-from-azure.sh):

| Option             | What it does                                                                                |
| ------------------ | ------------------------------------------------------------------------------------------- |
| `--move`           | Leaves the Azure API and Keycloak stopped afterwards, for the final move                    |
| `--no-freeze`      | Keeps Azure running while it copies (writes during the copy can be lost). Not with `--move` |
| `--yes`            | Doesn't ask you to type `MIGRATE` first                                                     |
| `--azure-env FILE` | The Azure settings file. Default `deploy/azure/.env`                                        |
| `--dump-dir DIR`   | Where the database dumps go. Default `~/buddy-backups/azure-migration-<timestamp>/`         |
| `-h`, `--help`     | Lists the options                                                                           |

The whole procedure is in [Moving from Azure](../deploy/README.md#10-moving-from-azure).

### Preflight checks

`task deploy`, `task deploy:pull` and the Azure migration first run
[`preflight.sh`](../deploy/preflight.sh). It refuses to continue when:

- it runs inside a container (the devcontainer, Codespaces or a `docker exec` shell), where it
  would start production on the wrong Docker and ask for real certificates;
- `DEPLOY_HOST` isn't this machine's `hostname`;
- `POSTGRES_PASSWORD` or `KEYCLOAK_ADMIN_PASSWORD` is still `change-me`.

It only warns when `KEYCLOAK_ADMIN_CLI_SECRET` is still the placeholder, or when a newer release
than the one you're deploying exists. There is no option to skip it; to deploy to another
machine, set `DEPLOY_HOST` in that machine's `.env`.

### API commands

Besides running the web server (its default), the API image has maintenance commands. Run them in
a one-off container from `deploy/`, with `docker compose -f docker-compose.prod.yml run --rm api
<command>`, and stop the API first for the ones that change data. `help` lists them all and
`help <command>` shows one command's options.

| Command                                         | What it does                                                                                        |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `check-env`                                     | Runs the API's environment checks against the current settings, then exits                          |
| `describe`                                      | Prints the API's configuration, stores and handlers                                                 |
| `db-assert`                                     | Fails if the database schema doesn't match this version                                             |
| `projections list`                              | Lists the event stores                                                                              |
| `projections rebuild --store marten://<store>/` | Rebuilds a store's snapshots from its events, e.g. after an upgrade that adds a snapshot projection |

See "Snapshots" in the [deploy notes](../deploy/README.md#notes) for when a rebuild is needed.

### Other scripts

| Script                                                  | What it does                                                                                     |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| [`deploy/version.sh`](../deploy/version.sh)             | Prints the version from git tags, as built into the images. `--commit` prints the commit instead |
| [`deploy/release-check.sh`](../deploy/release-check.sh) | Warns when a newer release exists. Run by the preflight                                          |

## After installing

- **Privacy.** You are the data controller for the children's data in your Buddy.
  [Setting up privacy](privacy-setup.md) walks through what that asks of you.
- **Backups.** There is no automatic backup. Take `pg_dump`s of both databases before every
  upgrade, copy them off the server, and delete them after 30 days; see
  [Backups and restore](../deploy/README.md#7-backups-and-restore).
- **Upgrades.** Watch the GitHub repository's releases, take a backup, check out the new tag and
  run `task deploy:pull` again; see [Staying up to date](../deploy/README.md#9-staying-up-to-date).
  A release that adds new event types can't be rolled back without restoring the backup.
- **Rollback.** See [Rollback](../deploy/README.md#8-rollback).
- **Installing on a child's tablet.** See [Installing Buddy on a kid's iPad](frontend/analysis/ipad-installation.md).
