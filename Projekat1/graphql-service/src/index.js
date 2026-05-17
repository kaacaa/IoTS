import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import { typeDefs } from './schema.js';
import { pool } from './db.js';

const resolvers = {
  Query: {
    // Scenario B
    readings: async (_, { device_id, limit }) => {
      const res = await pool.query(
        `SELECT * FROM sensor_readings
         WHERE device_id = $1
         ORDER BY ts DESC LIMIT $2`,
        [device_id || 'b8:27:eb:bf:9d:51', limit || 100]
      );
      return res.rows;
    },

    // Scenario C
    aggregates: async (_, { device_id, from, to }) => {
      const res = await pool.query(
        `SELECT
           DATE_TRUNC('hour', ts) AS hour,
           AVG(temp)              AS avg_temp,
           AVG(humidity)          AS avg_humidity,
           AVG(co)                AS avg_co,
           AVG(smoke)             AS avg_smoke,
           COUNT(*)               AS num_readings
         FROM sensor_readings
         WHERE device_id = $1
           AND ts >= $2::timestamptz
           AND ts <= $3::timestamptz
         GROUP BY hour
         ORDER BY hour DESC`,
        [
          device_id || 'b8:27:eb:bf:9d:51',
          from      || '2020-07-12',
          to        || '2026-05-17'
        ]
      );
      return res.rows;
    },

    allReadings: async (_, { limit, offset }) => {
      const res = await pool.query(
        `SELECT * FROM sensor_readings ORDER BY ts DESC LIMIT $1 OFFSET $2`,
        [limit || 50, offset || 0]
      );
      return res.rows;
    },
  },

  Mutation: {
    // Scenario A
    ingestReading: async (_, args) => {
      const { device_id, co, humidity, light, lpg, motion, smoke, temp } = args;
      const res = await pool.query(
        `INSERT INTO sensor_readings
         (ts, device_id, co, humidity, light, lpg, motion, smoke, temp)
         VALUES (NOW(), $1, $2, $3, $4, $5, $6, $7, $8)
         RETURNING id, ts`,
        [device_id, co, humidity, light, lpg, motion, smoke, temp]
      );
      const row = res.rows[0];
      return { id: row.id, ts: row.ts.toISOString() };
    },
  },
};

const server = new ApolloServer({ typeDefs, resolvers });
const { url } = await startStandaloneServer(server, {
  listen: { port: 4000 }
});
console.log(`GraphQL server pokrenut na: ${url}`);