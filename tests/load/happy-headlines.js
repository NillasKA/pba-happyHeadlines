import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:8080';
const articleId = __ENV.ARTICLE_ID;
const region = __ENV.REGION || 'eu';

export const options = {
  vus: 10,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

export default function () {
  const headers = { 'X-Region': region };
  const articlePath = articleId ? `/articles/${articleId}` : `/articles?region=${region}`;
  const article = http.get(`${baseUrl}${articlePath}`, { headers });
  check(article, { 'article request succeeds': (response) => response.status === 200 });

  if (articleId) {
    const comments = http.get(`${baseUrl}/articles/${articleId}/comments`, { headers });
    check(comments, { 'comment request succeeds': (response) => response.status === 200 });
  }
  sleep(0.2);
}
