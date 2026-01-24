import express from 'express';
import cookieParser from 'cookie-parser';

import { buildIssuerRouter } from './src/issuerRouter.js';

const app = express();
app.use(cookieParser());
app.use(express.urlencoded({ extended: false }));
app.use(express.json());

app.get('/', (req, res) => {
  res.type('text').send('Simulated OIDC IdP-B running. Try /.well-known/openid-configuration');
});

app.use('/', buildIssuerRouter({
  issuerDisplayName: 'Issuer B',
  userSet: 'B'
}));

const port = parseInt(process.env.PORT ?? process.env.IDP_PORT ?? '9002', 10);
app.listen(port, () => {
  console.log(`Simulated OIDC IdP-B listening on http://localhost:${port}`);
});
