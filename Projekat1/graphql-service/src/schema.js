export const typeDefs = `#graphql
  type SensorReading {
    id:        ID!
    ts:        String!
    device_id: String!
    co:        Float
    humidity:  Float
    light:     Boolean
    lpg:       Float
    motion:    Boolean
    smoke:     Float
    temp:      Float
  }

  type AggregateData {
    hour:         String!
    avg_temp:     Float
    avg_humidity: Float
    avg_co:       Float
    avg_smoke:    Float
    num_readings: Int
  }

  type IngestResult {
    id: ID!
    ts: String!
  }

  type Query {
    # Scenario B — klijent bira KOJA polja hoce
    readings(device_id: String, limit: Int): [SensorReading]

    # Scenario C — agregacije
    aggregates(device_id: String, from: String, to: String): [AggregateData]

    # Opsti listing
    allReadings(limit: Int, offset: Int): [SensorReading]
  }

  type Mutation {
    # Scenario A — upis
    ingestReading(
      device_id: String!
      co:        Float
      humidity:  Float
      light:     Boolean
      lpg:       Float
      motion:    Boolean
      smoke:     Float
      temp:      Float
    ): IngestResult
  }
`;