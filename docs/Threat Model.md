# Threat Model: Zero Trust Architecture for Bio-Research Data Exchange

**Document Version:** 1.0  
**Methodology:** STRIDE (Microsoft) & NIST SP 800-207  
**Context:** Secure Inter-Organizational Data Exchange of Genomic/Clinical Assets

---

## 1. Introduction and Scope

This document defines the architectural threat model for the Zero Trust Bio-Research Data Platform. Unlike traditional perimeter-based models, this analysis assumes the network is hostile and focuses on **Identity**, **Data Governance**, and **Non-Repudiation**.

The primary objective is to identify how the proposed **AWS Cloud-Native Architecture** mitigates specific risks associated with the "Anonymization Paradox" and the "De-provisioning Gap" in academic/clinical collaboration.

### 1.1 The Protected Asset
The core asset is **Genomic and Clinical Research Data**.
*   **Sensitivity:** High (GDPR/HIPAA implications).
*   **Risk Profile:** Vulnerable to "Linkage Attacks" (Re-identification) and unauthorized retention by former collaborators.

---

## 2. Architecture & Trust Boundaries

The system relies on a **Hub-and-Spoke** federation model. Trust is ephemeral and evaluated per request.

### 2.1 Trust Boundaries
1.  **The Identity Boundary (External):** The demarcation between the Platform (AWS Cognito) and external Identity Providers (University/Hospital IdPs). The platform trusts the *federated claim*, not the user directly.
2.  **The Application Boundary (Control Plane):** The API Gateway and Step Functions State Machine that orchestrate logic.
3.  **The Data Boundary (Data Plane):** The S3 Bucket containing encrypted genomic datasets, accessible only via signed ephemeral URLs.

---

## 3. Threat Actors (Adversary Model)

*   **A-01: The Orphaned Collaborator:** A researcher who has left their institution but attempts to use their credentials (which may not yet be disabled) or cached access to retrieve data.
*   **A-02: The Repudiating Actor:** A valid user who downloads sensitive data, leaks it, and subsequently denies having performed the action ("It wasn't me").
*   **A-03: The Insider (Data Owner):** A privileged user who attempts to bypass governance controls to exfiltrate data without an audit trail.
*   **A-04: The External Attacker:** An entity attempting to intercept data in transit or harvest valid pre-signed URLs.

---

## 4. STRIDE Analysis

The following analysis maps threats to the **STRIDE** framework, specifically analyzing the proposed AWS architecture.

### T-01: Spoofing (Identity & Authentication)
**Threat:** **Identity Retention / The "Orphaned Account"**
*   **Scenario:** A collaborator leaves their organization. The home organization (IdP) takes 24-48 hours to update their directory. The user attempts to access the platform during this window.
*   **Target:** AWS Cognito / OIDC Interface.
*   **Impact:** Unauthorized access by a non-affiliated individual.
*   **Architectural Mitigation (ZTA):**
    *   **Federated Session Duration:** Set strict OIDC token limits (e.g., 1 hour).
    *   **Real-Time Assessment:** The platform relies on JIT (Just-in-Time) claims. If the IdP account is disabled, the federation handshake fails immediately. *Mitigates RISK-001.*

### T-02: Tampering (Integrity)
**Threat:** **Metadata Modification / "Kill Switch" Bypass**
*   **Scenario:** An actor with write access attempts to modify S3 object tags (e.g., changing `status:blocked` to `status:active`) to bypass the automated "Active Defense" system.
*   **Target:** S3 Object Metadata.
*   **Impact:** Exfiltration of files flagged by AWS Macie as sensitive/malicious.
*   **Architectural Mitigation (ZTA):**
    *   **Role Segregation:** IAM policies separate the `Human_Admin` role from the `System_Orchestrator` role.
    *   **Condition Keys:** `s3:PutObjectTagging` is denied for human users via Service Control Policies (SCPs) or boundary policies. Only the Step Function role can alter governance tags.

### T-03: Repudiation (Accountability)
**Threat:** **The "Legal Shield" Failure**
*   **Scenario:** A user downloads a dataset and later claims their session was hijacked or they never requested the file.
*   **Target:** Audit Logs / Chain of Custody.
*   **Impact:** Legal liability for the Data Custodian; inability to prove compliance.
*   **Architectural Mitigation (ZTA):**
    *   **Step Function Orchestration:** The system enforces a **Multi-Factor Handshake**.
        1.  User Intent (API Request).
        2.  Proof of Possession (Out-of-Band Token sent to Email).
        3.  Action (User submits Token).
    *   **Evidence:** CloudWatch Logs record the `Token_Match` event, cryptographically linking the requestor to the download action. *Mitigates RISK-003.*

### T-04: Information Disclosure (Confidentiality)
**Threat:** **URL Persistence & Leakage**
*   **Scenario:** A valid pre-signed S3 URL is generated and sent via email/Slack. The user forwards this link to unauthorized third parties, or the link remains valid in logs indefinitely.
*   **Target:** S3 Pre-Signed URLs.
*   **Impact:** Uncontrolled dissemination of genomic data.
*   **Architectural Mitigation (ZTA):**
    *   **Ephemeral Design:** URLs are generated with a strict `x-amz-expires` limit (e.g., 15 minutes).
    *   **No Persistence:** The URL is displayed *once* in the UI and never emailed.

### T-05: Denial of Service (Availability)
**Threat:** **Approval Workflow Saturation**
*   **Scenario:** An automated bot floods the API with access requests, triggering thousands of Step Function executions and "Approval Needed" emails, overwhelming Data Owners.
*   **Target:** API Gateway / Step Functions.
*   **Impact:** Cost spikes (DoS wallet attack) and operational paralysis.
*   **Architectural Mitigation:**
    *   **WAF & Throttling:** AWS WAF rate-limiting on the `/request` endpoint.
    *   **Token Buckets:** API Gateway usage plans restrict requests per IP/User.

### T-06: Elevation of Privilege (Authorization)
**Threat:** **Administrative Scope Creep**
*   **Scenario:** A "Data Owner" (intended only to approve files) attempts to onboard a new Partner Organization to facilitate collusion.
*   **Target:** Cognito User Groups / API Authorization.
*   **Impact:** Violation of Separation of Duties (SoD).
*   **Architectural Mitigation (ZTA):**
    *   **Cognito Group Mapping:**
        *   `Group: Admin` $\to$ Can invoke `Onboard_Org_Lambda`.
        *   `Group: DataOwner` $\to$ Can invoke `Approve_Request_Lambda`.
    *   **Logic:** The API uses `Cognito Authorizers` to strictly gate routes based on group membership. *Mitigates RISK-004.*

---

## 5. Zero Trust Architecture Alignment

This threat model ensures the system adheres to **NIST SP 800-207** principles:

| NIST Tenet | Implementation in Architecture |
| :--- | :--- |
| **1. All data sources and computing services are considered resources.** | S3 buckets are not trusted by default; access requires explicit temporary tokens. |
| **3. Access to individual enterprise resources is granted on a per-session basis.** | Pre-signed URLs are single-use/time-bound; OIDC tokens are short-lived. |
| **4. Access is determined by dynamic policy.** | Access depends on `User_State` (Federation) AND `Data_State` (S3 Metadata tags) at the moment of request. |
| **6. All resource authentication and authorization are dynamic and strictly enforced.** | Step Functions act as the Policy Decision Point (PDP) and Policy Enforcement Point (PEP) before any data access occurs. |

---

## 6. Conclusion

The threat analysis demonstrates that the proposed architecture moves security controls from the **Network Perimeter** to the **Identity and Application Layers**.

By addressing **Repudiation (T-03)** through orchestrated state machines and **Spoofing (T-01)** through strict federation, the system acts as a "Legal Shield," effectively mitigating the risks inherent in inter-organizational bio-research.

---