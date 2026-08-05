import { Link as RouterLink } from 'react-router';
import { Box, Button, CircularProgress, List, ListItemButton, ListItemText, Typography } from '@mui/material';

import { useTrips } from '../queries/trips';

/** Journey 1 entry point: every trip the signed-in user is a member of. */
export function TripsListPage() {
  const { data: trips, isLoading, isError } = useTrips();

  return (
    <Box data-testid="trips-list-page">
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h4" component="h2">
          Your trips
        </Typography>
        <Button variant="contained" component={RouterLink} to="/trips/new" data-testid="create-trip-link">
          Start a trip
        </Button>
      </Box>

      {isLoading && <CircularProgress aria-label="Loading trips" />}
      {isError && <Typography role="alert">We could not load your trips. Please try again.</Typography>}

      {trips && trips.length === 0 && (
        <Typography data-testid="no-trips-message">
          You are not planning any trips yet. Start one to invite your fellow travellers.
        </Typography>
      )}

      {trips && trips.length > 0 && (
        <List data-testid="trips-list">
          {trips.map((trip) => (
            <ListItemButton key={trip.id} component={RouterLink} to={`/trips/${trip.id}`} data-testid="trip-list-item">
              <ListItemText
                primary={trip.name}
                secondary={trip.destinations.length > 0 ? trip.destinations.join(', ') : 'No destination set yet'}
              />
            </ListItemButton>
          ))}
        </List>
      )}
    </Box>
  );
}

export default TripsListPage;
