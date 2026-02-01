// Initialize MongoDB collections and indexes for Zero Trust Data Exchange

db = db.getSiblingDB("zero_trust_db");

// Users collection
db.createCollection("users");
db.users.createIndex({ sub: 1 }, { unique: true });
db.users.createIndex({ organization_id: 1 });

// Roles collection
db.createCollection("roles");

// User Roles junction
db.createCollection("user_roles");
db.user_roles.createIndex({ user_id: 1 });

// Organizations collection
db.createCollection("organizations");
db.organizations.createIndex({ cognito_group_name: 1 }, { unique: true });

// User Agreements collection
db.createCollection("user_agreements");
db.user_agreements.createIndex(
  { agreement_id: 1, version: 1 },
  { unique: true },
);

// Requests collection
db.createCollection("requests");
db.requests.createIndex({ requester_id: 1, created_at: -1 });
db.requests.createIndex({ request_status: 1 });
db.requests.createIndex({ data_owner_organization_id: 1, request_status: 1 });

// Audit Events collection (append-only log)
db.createCollection("audit_events");
db.audit_events.createIndex({ event_type: 1 });
db.audit_events.createIndex({ request_id: 1 });
db.audit_events.createIndex({ organization_id: 1 });
db.audit_events.createIndex({ created_at: -1 });

print("MongoDB initialization complete!");
