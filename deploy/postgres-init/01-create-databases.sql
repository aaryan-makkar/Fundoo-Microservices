CREATE DATABASE fundoo_notes;

CREATE TABLE IF NOT EXISTS "PasswordResetTokens" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "UsedAtUtc" timestamp with time zone
);

CREATE TABLE IF NOT EXISTS "Users" (
    "Id" uuid PRIMARY KEY,
    "FirstName" text NOT NULL,
    "LastName" text NOT NULL,
    "Email" varchar(256) NOT NULL,
    "NormalizedEmail" varchar(256) NOT NULL,
    "PasswordHash" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_NormalizedEmail"
    ON "Users" ("NormalizedEmail");

\connect fundoo_notes

CREATE TABLE IF NOT EXISTS notes (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    title text NOT NULL,
    content text NOT NULL,
    color text,
    is_pinned boolean NOT NULL DEFAULT false,
    is_archived boolean NOT NULL DEFAULT false,
    is_trashed boolean NOT NULL DEFAULT false,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS labels (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    name text NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_labels_user_lower_name
    ON labels (user_id, lower(name));

CREATE TABLE IF NOT EXISTS note_labels (
    note_id uuid NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    label_id uuid NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
    PRIMARY KEY (note_id, label_id)
);
