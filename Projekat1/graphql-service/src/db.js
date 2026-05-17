import pg from 'pg';
const { Pool } = pg;

export const pool = new Pool({
  host: process.env.DB_HOST || 'postgres',
  port: process.env.DB_PORT || 5432,
  database: process.env.DB_NAME || 'iotdb',
  user: process.env.DB_USER || 'iotuser',
  password: process.env.DB_PASS || 'iotpass',
});