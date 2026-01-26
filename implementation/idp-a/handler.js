import express from "express";
import cookieParser from "cookie-parser";

import serverless from "serverless-http";
import { buildIssuerRouter } from "./src/issuerRouter.js";

const app = express();
app.use(cookieParser());
app.use(express.urlencoded({ extended: false }));
app.use(express.json());

app.get("/", (req, res) => {
  res
    .type("text")
    .send(
      "Simulated OIDC IdP-A running. Try /.well-known/openid-configuration",
    );
});

app.use(
  "/",
  buildIssuerRouter({
    issuerDisplayName: "Issuer A",
    userSet: "A",
  }),
);

// Export handler for AWS Lambda Web Adapter
// Export handler using serverless-http wrapper for AWS Lambda
export const handler = serverless(app);
