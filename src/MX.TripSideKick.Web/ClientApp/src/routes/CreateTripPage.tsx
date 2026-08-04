import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import {
  Alert,
  Box,
  Button,
  MenuItem,
  Stack,
  TextField,
  Typography
} from '@mui/material';

import { useCreateTrip } from '../queries/trips';
import type { TripDateStatus } from '../api/roles';

const dateStatuses: { value: TripDateStatus; label: string }[] = [
  { value: 'undecided', label: 'Undecided - just an idea for now' },
  { value: 'approximate', label: 'Approximate - roughly know when' },
  { value: 'confirmed', label: 'Confirmed - exact dates' }
];

const createTripSchema = z
  .object({
    name: z.string().trim().min(1, 'Give the trip a name.'),
    destinationsText: z.string(),
    reportingCurrencyCode: z
      .string()
      .trim()
      .toUpperCase()
      .refine((value) => value === '' || /^[A-Z]{3}$/.test(value), 'Use a 3-letter ISO 4217 currency code, e.g. USD.'),
    dateStatus: z.enum(['undecided', 'approximate', 'confirmed']),
    startDate: z.string(),
    endDate: z.string(),
    coverImageUrl: z.string().trim()
  })
  .refine((data) => data.dateStatus !== 'confirmed' || (data.startDate !== '' && data.endDate !== ''), {
    message: 'Confirmed trips need both a start and an end date.',
    path: ['startDate']
  })
  .refine((data) => data.startDate === '' || data.endDate === '' || data.startDate <= data.endDate, {
    message: 'The start date must be on or before the end date.',
    path: ['endDate']
  });

type CreateTripFormValues = z.infer<typeof createTripSchema>;

const defaultValues: CreateTripFormValues = {
  name: '',
  destinationsText: '',
  reportingCurrencyCode: '',
  dateStatus: 'undecided',
  startDate: '',
  endDate: '',
  coverImageUrl: ''
};

/** Journey 1: "Start a trip" - name is the only required field, everything else can stay incomplete. */
export function CreateTripPage() {
  const navigate = useNavigate();
  const createTrip = useCreateTrip();
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting }
  } = useForm<CreateTripFormValues>({
    resolver: zodResolver(createTripSchema),
    defaultValues
  });

  const dateStatus = watch('dateStatus');

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null);

    try {
      const trip = await createTrip.mutateAsync({
        name: values.name.trim(),
        destinations:
          values.destinationsText.trim() === ''
            ? null
            : values.destinationsText
                .split(',')
                .map((destination) => destination.trim())
                .filter((destination) => destination.length > 0),
        reportingCurrencyCode: values.reportingCurrencyCode === '' ? null : values.reportingCurrencyCode,
        dates:
          values.dateStatus === 'undecided'
            ? { status: 'undecided', startDate: null, endDate: null }
            : { status: values.dateStatus, startDate: values.startDate || null, endDate: values.endDate || null },
        coverImageUrl: values.coverImageUrl === '' ? null : values.coverImageUrl
      });

      navigate(`/trips/${trip.id}`);
    } catch {
      setSubmitError('We could not create the trip. Please try again.');
    }
  });

  return (
    <Box component="form" onSubmit={onSubmit} data-testid="create-trip-form" noValidate>
      <Typography variant="h4" component="h2" gutterBottom>
        Start a trip
      </Typography>

      <Stack spacing={2} sx={{ maxWidth: 480 }}>
        {submitError && <Alert severity="error">{submitError}</Alert>}

        <Controller
          name="name"
          control={control}
          render={({ field }) => (
            <TextField
              {...field}
              label="Trip name"
              required
              autoFocus
              error={Boolean(errors.name)}
              helperText={errors.name?.message}
              slotProps={{ htmlInput: { 'data-testid': 'trip-name-input' } }}
            />
          )}
        />

        <Controller
          name="destinationsText"
          control={control}
          render={({ field }) => (
            <TextField
              {...field}
              label="Destinations (optional, comma-separated)"
              slotProps={{ htmlInput: { 'data-testid': 'trip-destinations-input' } }}
            />
          )}
        />

        <Controller
          name="reportingCurrencyCode"
          control={control}
          render={({ field }) => (
            <TextField
              {...field}
              label="Reporting currency (optional, ISO 4217, e.g. USD)"
              error={Boolean(errors.reportingCurrencyCode)}
              helperText={errors.reportingCurrencyCode?.message}
              slotProps={{ htmlInput: { 'data-testid': 'trip-currency-input', maxLength: 3 } }}
            />
          )}
        />

        <Controller
          name="dateStatus"
          control={control}
          render={({ field }) => (
            <TextField {...field} select label="Dates" slotProps={{ select: { 'data-testid': 'trip-date-status-select' } as never }}>
              {dateStatuses.map((option) => (
                <MenuItem key={option.value} value={option.value}>
                  {option.label}
                </MenuItem>
              ))}
            </TextField>
          )}
        />

        {dateStatus !== 'undecided' && (
          <Stack direction="row" spacing={2}>
            <Controller
              name="startDate"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  type="date"
                  label="Start date"
                  slotProps={{ inputLabel: { shrink: true }, htmlInput: { 'data-testid': 'trip-start-date-input' } }}
                  error={Boolean(errors.startDate)}
                  helperText={errors.startDate?.message}
                />
              )}
            />
            <Controller
              name="endDate"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  type="date"
                  label="End date"
                  slotProps={{ inputLabel: { shrink: true }, htmlInput: { 'data-testid': 'trip-end-date-input' } }}
                  error={Boolean(errors.endDate)}
                  helperText={errors.endDate?.message}
                />
              )}
            />
          </Stack>
        )}

        <Controller
          name="coverImageUrl"
          control={control}
          render={({ field }) => <TextField {...field} label="Cover image URL (optional)" />}
        />

        <Button type="submit" variant="contained" disabled={isSubmitting} data-testid="submit-create-trip">
          Create trip
        </Button>
      </Stack>
    </Box>
  );
}

export default CreateTripPage;
