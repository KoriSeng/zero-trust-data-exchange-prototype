# Investigation into Zero Trust Architectures for Secure Inter-Organizational Data Exchange in Bio-Research Environments

## **1. Abstract and Research Context**

In the high-consequence domain of bio-research, datasets—including genomic sequences and longitudinal clinical records—represent **high-consequence digital assets**, often referred to as "Digital Gold." The sensitivity of this data is compounded by the **Anonymization Paradox**, where seemingly de-identified records remain vulnerable to **linkage attacks**. By cross-referencing "anonymous" records with external datasets, malicious actors can re-identify individuals, making the exposure of such data a permanent and irreversible risk. Because the host organization (the custodian) effectively loses physical control of the data once a download occurs, the security architecture must shift its focus from traditional perimeter defense to a model of **Operational De-risking**. This requires a transition from "Implicit Trust" to a **Zero Trust (ZT)** framework where the legality, identity, and intent of the receiver are verified continuously before any data leaves the secure environment.

The primary technical challenge investigated in this project is the **"De-provisioning Gap"** inherent in inter-organizational collaboration. Most contemporary research environments manage external access through **Static Identity Provisioning**, where local accounts are manually created for collaborators. However, the custodian lacks integration with the partner organization’s HR systems and is rarely notified when a collaborator’s employment status changes. This leads to the proliferation of **orphaned accounts**—static credentials that persist as unmonitored backdoors. Furthermore, legacy sharing methods like email and SFTP lack granular, object-level control, allowing data to persist in intermediate caches indefinitely. This research investigates the application of **Federated Identity Management (FIM)** and **Just-in-Time (JIT) orchestration** as a means to close the identity lifecycle gap. By delegating authentication to the partner’s own Identity Provider and implementing surgical, metadata-driven revocation, organizations can ensure that data sharing is intentional, time-bound, and legally defensible.

---

## 2. Proposed Project Artefact

The project deliverable is a functional, cloud-native **Zero Trust Data Sharing Platform** designed to facilitate secure, governed exchange of high-value research data. The artefact consists of a web-based portal integrated with a serverless backend architecture on **Amazon Web Services (AWS)**. The platform acts as a "Legal and Technical Shield," ensuring that data access is never persistent and remains tied to both verified identity and explicit business intent.

The system is architected across four functional layers:

1.  **Federated Identity Gateway:** Utilizing **AWS Cognito**, the platform replaces local account management with federated SSO (OIDC/SAML). This allows the host organization to federate external partner Identity Providers (IdPs), ensuring that access is automatically revoked the moment a collaborator is disabled by their parent organization, thus closing the "de-provisioning gap."
2.  **Serverless Governance Orchestration:** An **AWS Step Functions** workflow manages the lifecycle of a data request. This includes administrative onboarding (verifying NDAs) and a "Human-in-the-Loop" approval process where data owners must explicitly authorize a request before access artefacts are generated.
3.  **Ephemeral Delivery Layer:** To prevent data persistence in intermediate caches, the platform utilizes **short-lived S3 Pre-signed URLs**. A unique **Out-of-Band Redemption** mechanism is implemented: upon approval, a one-time code is sent to the user’s verified email. The user must redeem this code within the application to generate the time-bound download link.
4.  **Surgical Revocation & Defense:** The system implements an **"Object-Level Kill Switch."** By integrating **Amazon Macie** for PII detection and manual administrative triggers, the platform can update **S3 Object Metadata** to "poison" specific files. An S3 Bucket Policy acts as the **Policy Enforcement Point (PEP)**, blocking the generation of pre-signed URLs for any object flagged as compromised or revoked.

**Platform and Technology Stack:**
*   **Cloud Infrastructure:** Amazon Web Services (AWS).
*   **Security Services:** AWS WAF (Web Application Firewall), Amazon GuardDuty (Malware Protection), and AWS Macie (Sensitive Data Discovery).
*   **Compute & Logic:** AWS Lambda (Python/Node.js) and AWS Step Functions.
*   **Identity:** AWS Cognito User Pools (Federated IdP integration).
*   **Storage:** Amazon S3 with Object Tagging and Bucket Policies.
*   **Client Interface:** A responsive web application (React or Vue.js) to serve as the user and administrative portal.
*   **Audit & Logging:** AWS CloudWatch and CloudTrail to provide an immutable, forensic record of all access decisions.

---

## 3. Project Rationale

The choice of this project is driven by a convergence of professional expertise, academic interest, and the urgent need to solve a critical security challenge within the bio-research sector. Having observed the operational complexities of data sharing within my current organization, I identified a recurring conflict between the need for rapid collaboration and the necessity of robust **Information Governance**. Current legacy systems fail to bridge this gap, often leading to a compromise in security to achieve collaborative efficiency. This project provides a vehicle to investigate how **Security-by-Design** can resolve this friction through a Zero Trust framework.

The project aligns directly with my career aspirations as a **Cloud Security Solutions Architect**. It allows me to apply advanced architectural principles—such as the **AWS Well-Architected Framework’s Security Pillar**—to a high-stakes environment where data is a "high-consequence asset." Furthermore, it synthesizes key concepts from modules such as **Information Governance** and **Network Security**, specifically regarding non-repudiation, the legal chain of custody, and identity federation.

My background as a **Professional Solutions Architect and Full Stack Developer** provides me with the technical depth required to deliver a functional, end-to-end cloud-native prototype. Beyond the technical implementation, I am utilizing **PMP-inspired project governance** to manage this investigation. This includes the development of formal project artefacts—such as a Project Charter, Risk Register, and Control Catalogue—ensuring that the investigation is conducted with a level of professional rigour that matches the sensitivity of the bio-research domain. Ultimately, this project allows me to demonstrate the ability to deliver tangible value by solving a complex, real-world cybersecurity problem through architectural innovation.

---

## 4. Background Research

The background research for this project is grounded in the analysis of structural failures in **Identity Lifecycle Management (ILM)** and the limitations of traditional "Implicit Trust" perimeters.

A primary driver for this project is the documented risk of **"De-provisioning Gaps."** Recent high-profile incidents, such as the unauthorized access and system deletion by a former employee at a major technology firm (NCS), highlight how "human oversight" in manual offboarding can lead to catastrophic security breaches (CNA, 2024). While that case involved an internal employee, the risk is significantly magnified in inter-organizational bio-research collaborations where the host organization has zero visibility into a partner's HR status. My research confirms that legacy sharing methods—manual account creation, email, and SFTP—rely entirely on these flawed manual processes.

Furthermore, my investigation into existing enterprise solutions, such as **Microsoft SharePoint (Azure AD B2B)**, revealed a technical deadlock for decentralized research organizations. These products typically require deep directory-level trust or synchronization. In multi-entity research environments where labs operate as semi-autonomous subsidiaries, central IT policies often prohibit such integration to maintain security perimeters. This research identifies a market and technical gap for **"Identity-Agnostic Governance"**—a system that can verify external identities via federation (OIDC/SAML) without requiring infrastructure-level directory trust.

Finally, I have reviewed the **NIST SP 800-207 (Zero Trust Architecture)** standard and literature on the **"Anonymization Paradox."** These resources confirm that because bio-research data is susceptible to **linkage attacks** (re-identification), the security focus must shift from "defending the file" to "governing the identity and intent" of the requester.

---

## 5. Areas for Investigation

While the foundational research is complete, the execution of this project requires deep-dive investigations into several complex technical and legal domains:

1.  **Attribute-Based Access Control (ABAC) Mapping:** I will investigate how to securely map "claims" from an external Identity Provider (e.g., an organization ID or project role) to dynamic internal AWS IAM policies. The goal is to determine how to enforce permissions based on *attributes* rather than *identity names*.
2.  **Thresholds for Ephemeral Access:** I will investigate the security trade-offs regarding the "Time-to-Live" (TTL) for **S3 Pre-signed URLs**. This involves determining the optimal balance between user experience (giving enough time to download large genomic files) and security (minimizing the window of exposure).
3.  **Automated Metadata Gating (The "Kill Switch"):** A core area of investigation is the technical implementation of **Object-Level Revocation**. I will research how to programmatically trigger S3 metadata updates via **AWS Macie** or administrative APIs to act as a "Circuit Breaker," effectively "poisoning" a file's retrieval path without deleting the object itself.
4.  **Non-Repudiation and Forensic Audit Trails:** I will investigate how to correlate disparate logs from **AWS Cognito, Step Functions, and S3** into a single, immutable "Chain of Custody." The investigation will focus on proving **Non-Repudiation**: ensuring that a specific federated user cannot deny having retrieved a specific dataset.
5.  **Out-of-Band Verification Workflows:** I will explore the effectiveness of **One-Time Redemption Codes** as a second factor in the JIT workflow. The investigation will focus on whether this out-of-band step successfully mitigates session hijacking and federation spoofing risks.

---

## 6. Research Ethics

The ethical considerations for this project focus primarily on **Data Privacy**, **Organizational Anonymity**, and the **Integrity of Simulated Environments**. As this project involves the design of a system to protect high-consequence bio-research data, the following ethical safeguards will be strictly enforced:

1.  **Organizational Anonymity:** To protect the proprietary processes and security posture of existing entities, no specific organizations, laboratories, or subsidiaries will be named or identified within the project documentation. The host organization and its partners will be referred to using generic archetypes (e.g., "The Data Custodian" and "External Collaborator").
2.  **Use of Synthetic Datasets:** No real-world bio-research data, genomic sequences, clinical trial results, or Personally Identifiable Information (PII) will be utilized at any stage of the project. Instead, I will use **synthetic (dummy) datasets**. These files will be engineered to mimic the "shape and form" (file extensions, data structures, and sizes) of real research data to demonstrate the system's technical viability without compromising the privacy of any actual individuals or research efforts.
3.  **Infrastructure Isolation:** The prototype will be implemented within an independent, personal AWS environment. It will not utilize any corporate cloud services, internal networks, or proprietary infrastructure belonging to my current employer. This ensures a clear boundary between my professional responsibilities and academic research.
4.  **Security Responsibility:** While the project involves investigating potential vulnerabilities (such as "The De-provisioning Gap"), all "Red Team" testing and simulated attacks will be conducted exclusively within this isolated, synthetic environment. No testing will be performed against real-world systems or external third-party services.
5.  **Ethical Data Stewardship:** The core aim of this project is to enhance the ethical stewardship of sensitive information. By investigating **Non-Repudiation** and **Just-in-Time access**, the project seeks to contribute to the development of safer, more accountable frameworks for sharing data that—if leaked—could have significant societal and ethical consequences.

---

### 7. Review of Reference Materials

### **Books**
*   **Ward, E. and Smith, B. (2021) *Zero Trust Networks: Building Secure Systems in Untrusted Networks*. 2nd edn. Sebastopol: O'Reilly Media.**
    *   *Justification:* This is considered a seminal work in the Zero Trust space. It provides the core theoretical framework for shifting security away from network perimeters toward identity-centric models. It will be used to define the fundamental architectural principles of the proposed prototype.
*   **Garbis, J. and Chapman, J. (2021) *Zero Trust Security: An Enterprise Guide*. Berkeley: Apress.**
    *   *Justification:* This book focuses on practical enterprise-scale implementation of Zero Trust. It is highly relevant to this project as it discusses modern Identity and Access Management (IAM) governance and the transition from legacy VPNs to more agile, identity-aware proxies.

### **Academic Papers / Industry Standards**

*   **Rose, S., Borchert, O., Mitchell, S. and Connelly, S. (2020) *Zero Trust Architecture (NIST Special Publication 800-207)*. Gaithersburg: National Institute of Standards and Technology.**
    Justification: This is the globally recognized industry-standard reference for Zero Trust. It defines the "Policy Decision Point" (PDP) and "Policy Enforcement Point" (PEP) components that my AWS prototype will implement. It provides the academic and technical rigour required for a security investigation.

*   **Pulivarti, R., Martin, N., Byers, F., Wagner, J., Zook, J., Maragh, S., Martin, K., Wojtyniak, M., Kreider, B. and France, A. (2023) Cybersecurity of Genomic Data (NIST Internal Report 8432). Gaithersburg: National Institute of Standards and Technology.**
    Justification: This industry-standard reference work provides a detailed analysis of the genomic data life cycle and associated cybersecurity threats. It justifies the project’s focus on the "Dissemination" phase and the need for bespoke security controls for genomic-DNA data sharing.

*   **Sannigrahi, M., Ray, N. K., Gupta, R. and Alazab, M. (2024) *‘Zero trust security for healthcare cloud using a lightweight authentication’*, Cluster Computing. Springer [Online].**
    Justification: This peer-reviewed journal article reports on a research study regarding Zero Trust implementation specifically for healthcare cloud environments. It provides an up-to-date academic model (2024) for lightweight authentication that informs the design of the project’s JIT redemption workflow.

*   **Almadhoun, N., Kadri, A. and Al-Aswad, N. H. (2025) *‘A review on secure medical data sharing and permission management using zero-trust’*, Informatics in Medicine Unlocked, 101684. ScienceDirect [Online]**.
    Justification: This recent (2025) journal paper provides a comprehensive review of medical data sharing using Zero Trust. It provides the academic rationale for the project’s investigation into permission management and identity-centric access in clinical research environments.

### **Websites**
*   **Amazon Web Services (2023) *Security Pillar: AWS Well-Architected Framework*. Available at: https://docs.aws.amazon.com/wellarchitected/latest/security-pillar/welcome.html (Accessed: 20 January 2026).**
    *   *Justification:* This is the definitive industry guide for building secure cloud architectures. It provides the best-practice patterns for IAM, detective controls, and data protection that will guide the implementation of the AWS prototype.
*   **Channel News Asia (2024) *Former NCS employee sentenced to jail for deleting servers after being fired*. Available at: https://www.channelnewsasia.com/singapore/former-ncs-employee-access-computer-system-delete-servers-human-oversight-4405131 (Accessed: 20 January 2026).**
    *   *Justification:* This news report provides a critical real-world case study of the "De-provisioning Gap." It justifies the project’s focus on automating the identity lifecycle to prevent the "human oversight" errors that lead to unauthorized access by former employees.
*   **SlashID (2023) *Identity security federation issues*. Available at: https://www.slashid.dev/blog/identity-security-federation-issues/ (Accessed: 20 January 2026).**
    *   *Justification:* This technical publication investigates advanced attack vectors in identity federation, such as IdP-spoofing. It informs the research area regarding out-of-band redemption codes and IdP-scoping as necessary secondary verification layers.

---

## 8. Methodology

This project follows a **Design Science Research (DSR)** methodology, which is an academic framework for investigating a problem through the creation and evaluation of a technical "artefact."

### **Development Strategy: Agile Iteration**
The technical prototype will be developed using an **Iterative Agile approach**. This allows for the incremental building of security controls and provides the flexibility to refine the Zero Trust architecture based on findings during each sprint.
*   **Benefits:** Supports "Security-by-Design" by allowing each layer (Identity, Governance, Delivery) to be independently tested.
*   **Drawbacks:** Risks "scope creep" which will be mitigated by maintaining a strict **Product Backlog** based on the primary research question.

### **Data Collection and Investigative Tasks**
To provide evidence for the research investigation, I will perform the following strategic tasks:
*   **Requirement Synthesis:** I am utilizing a **retrospective data collection** approach. The requirements for this platform were gathered through professional discovery sessions with representative stakeholders in the bio-research sector. These established needs serve as the baseline for the system requirements, ensuring the prototype solves a documented operational friction.
*   **Threat Modeling:** I will conduct a formal **STRIDE threat analysis** of legacy sharing models (manual accounts, email, SFTP). This qualitative data will be used to map specific vulnerabilities to the proposed Zero Trust controls.
*   **Evaluative Simulations (Red Team):** The primary data collected during the project will be **Qualitative Technical Telemetry**. I will simulate specific threat scenarios—such as unauthorized access attempts with disabled federated accounts—to collect forensic logs. These logs will be analyzed to verify the efficacy of the "Policy Enforcement Point" (PEP).
*   **Industry Validation:** I will perform a **Subject Matter Expert (SME) Review**. To ensure the prototype follows current cloud-native best practices, I intend to seek informal "sanity checks" from industry professionals, such as AWS Solutions Architects, against the **AWS Well-Architected Framework**.

---

## 9. Project Plan

The project is structured into four main phases over a 12-week period. Detailed task management and a GANTT chart (utilizing Microsoft Excel) will be maintained to track progress against these milestones.

| Phase | Milestone | Duration | Key Activities |
| :--- | :--- | :--- | :--- |
| **1. Analysis** | **Requirements & Threat Model** | Weeks 1-2 | Finalize risk-to-control mapping and STRIDE threat analysis. |
| **2. Design** | **Architecture Design Document** | Weeks 3-4 | Finalize AWS service selection, state-machine logic, and IdP-mapping. |
| **3. Implementation** | **Functional AWS Prototype** | Weeks 5-9 | **Sprint 1:** Identity Federation (Cognito). <br> **Sprint 2:** Governance Logic (Step Functions). <br> **Sprint 3:** Defensive Controls (S3 Gating). |
| **4. Evaluation** | **Final Investigation Report** | Weeks 10-12 | Run "Red Team" simulations, analyze telemetry, and write the final reflection. |

**Time Management Note:** As this project involves complex cloud-native development, I have factored in a "buffer" period during the implementation phase. Time spent on other academic modules and professional commitments is managed through a centralized calendar, with approximately 15–20 hours per week dedicated to project execution and documentation.

---


