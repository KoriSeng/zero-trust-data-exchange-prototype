#!/bin/bash

# Generate sample test data for Zero Trust Data Exchange
# This creates test organizations, users, and agreements

MONGO_URL="${MONGODB_URL:-mongodb://localhost:27017}"
DB_NAME="${DATABASE_NAME:-zero_trust_db}"

echo "Connecting to MongoDB at $MONGO_URL..."

# Create test organizations
mongosh "$MONGO_URL/$DB_NAME" << 'EOF'

// Create test roles
db.roles.insertMany([
  {
    _id: ObjectId(),
    role_name: "Requester",
    description: "Can request data access"
  },
  {
    _id: ObjectId(),
    role_name: "DataOwner",
    description: "Can approve data access requests"
  },
  {
    _id: ObjectId(),
    role_name: "Admin",
    description: "Full system administration"
  },
  {
    _id: ObjectId(),
    role_name: "Auditor",
    description: "Can view audit logs"
  }
]);

// Create test organizations
db.organizations.insertMany([
  {
    _id: "ORG-A",
    name: "Organization A (IDP-A)",
    cognito_idp_name: "IDP-A",
    cognito_group_name: "ap-southeast-1_xxxxx_IDP-A",
    nda_verified: true,
    nda_expires_at: new Date(Date.now() + 365*24*60*60*1000),
    status: "Active",
    created_at: new Date(),
    updated_at: new Date()
  },
  {
    _id: "ORG-B",
    name: "Organization B (IDP-B)",
    cognito_idp_name: "IDP-B",
    cognito_group_name: "ap-southeast-1_yyyyy_IDP-B",
    nda_verified: true,
    nda_expires_at: new Date(Date.now() + 365*24*60*60*1000),
    status: "Active",
    created_at: new Date(),
    updated_at: new Date()
  }
]);

// Create test users (JIT provisioned)
db.users.insertMany([
  {
    _id: ObjectId(),
    sub: "test-user-1@cognito",
    email: "alice@org-a.com",
    display_name: "Alice (Data Requester)",
    organization_id: "ORG-A",
    status: "Active",
    created_at: new Date(),
    updated_at: new Date(),
    last_login_at: new Date()
  },
  {
    _id: ObjectId(),
    sub: "test-user-2@cognito",
    email: "bob@org-b.com",
    display_name: "Bob (Data Owner)",
    organization_id: "ORG-B",
    status: "Active",
    created_at: new Date(),
    updated_at: new Date(),
    last_login_at: new Date()
  }
]);

// Create test user agreements
db.user_agreements.insertMany([
  {
    _id: ObjectId(),
    agreement_id: "AGREEMENT-001",
    version: 1,
    organization_id: "ORG-A",
    title: "Data Access Terms & Conditions v1.0",
    content: "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
      "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
      "This is a sample agreement for testing purposes.",
    status: "Active",
    created_at: new Date(),
    updated_at: new Date()
  }
]);

print("Sample data created successfully!");
EOF

echo "Sample test data loaded into MongoDB"
