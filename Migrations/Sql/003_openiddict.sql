-- OpenIddict 6.x (EF Core / Npgsql, string primary keys)
-- Run after 001_app_users.sql and 002_security_tables.sql

CREATE TABLE IF NOT EXISTS "OpenIddictApplications" (
    "Id" text NOT NULL,
    "ApplicationType" text,
    "ClientId" character varying(100),
    "ClientSecret" text,
    "ClientType" text,
    "ConcurrencyToken" character varying(50),
    "ConsentType" text,
    "DisplayName" text,
    "DisplayNames" text,
    "JsonWebKeySet" text,
    "Permissions" text,
    "PostLogoutRedirectUris" text,
    "Properties" text,
    "RedirectUris" text,
    "Requirements" text,
    "Settings" text,
    CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_OpenIddictApplications_ClientId"
    ON "OpenIddictApplications" ("ClientId");

CREATE TABLE IF NOT EXISTS "OpenIddictScopes" (
    "Id" text NOT NULL,
    "ConcurrencyToken" character varying(50),
    "Description" text,
    "Descriptions" text,
    "DisplayName" text,
    "DisplayNames" text,
    "Name" character varying(200),
    "Properties" text,
    "Resources" text,
    CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_OpenIddictScopes_Name"
    ON "OpenIddictScopes" ("Name");

CREATE TABLE IF NOT EXISTS "OpenIddictAuthorizations" (
    "Id" text NOT NULL,
    "ApplicationId" text,
    "ConcurrencyToken" character varying(50),
    "CreationDate" timestamp with time zone,
    "Properties" text,
    "Scopes" text,
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(50),
    CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId"
        FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type"
    ON "OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject", "Type");

CREATE TABLE IF NOT EXISTS "OpenIddictTokens" (
    "Id" text NOT NULL,
    "ApplicationId" text,
    "AuthorizationId" text,
    "ConcurrencyToken" character varying(50),
    "CreationDate" timestamp with time zone,
    "ExpirationDate" timestamp with time zone,
    "Payload" text,
    "Properties" text,
    "RedemptionDate" timestamp with time zone,
    "ReferenceId" character varying(100),
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(50),
    CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId"
        FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId"
        FOREIGN KEY ("AuthorizationId") REFERENCES "OpenIddictAuthorizations" ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_OpenIddictTokens_ReferenceId"
    ON "OpenIddictTokens" ("ReferenceId");

CREATE INDEX IF NOT EXISTS "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type"
    ON "OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");

CREATE INDEX IF NOT EXISTS "IX_OpenIddictTokens_AuthorizationId"
    ON "OpenIddictTokens" ("AuthorizationId");
