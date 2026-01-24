import express from 'express';
import cookieParser from 'cookie-parser';

import { buildIssuerRouter } from './src/issuerRouter.js';

const app = express();
app.use(cookieParser());
app.use(express.urlencoded({ extended: false }));
app.use(express.json());

app.get('/', (req, res) => {
  res.type('text').send('Simulated OIDC IdP-A running. Try /.well-known/openid-configuration');
});

app.use('/', buildIssuerRouter({
  issuerDisplayName: 'Issuer A',
  userSet: 'A'
}));

const port = parseInt(process.env.PORT ?? process.env.IDP_PORT ?? '9001', 10);
app.listen(port, () => {
  console.log(`Simulated OIDC IdP-A listening on http://localhost:${port}`);
});
