import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import axios from "axios";
import LocationPicker from "@/components/LocationPicker";
const CONNECTOR_TYPES = ["Type2", "CCS", "CHAdeMO", "Tesla"] as const;

const schema = z.object({
  name: z.string().min(1, "Name is required").max(120),
  address: z.string().optional(),
  lat: z.coerce.number().min(-90).max(90),
  lng: z.coerce.number().min(-180).max(180),
  connectorType: z.enum(CONNECTOR_TYPES),
  powerKw: z.coerce.number().positive("Power must be greater than 0"),
});

type FormValues = z.infer<typeof schema>;

export default function SubmitPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { register, handleSubmit, setValue, formState: { errors } } = useForm({
    resolver: zodResolver(schema),
  });

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const body = {
        name: values.name,
        address: values.address || null,
        lat: values.lat,
        lng: values.lng,
        operatorId: null,
        connectors: [{ type: values.connectorType, powerKw: values.powerKw, count: 1 }],
      };
      return (await api.post("/api/v1/stations", body)).data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["stations"] });
      navigate("/stations");
    },
  });

  const onSubmit = (values: FormValues) => mutation.mutate(values);

  return (
    <div className="p-4 max-w-md">
      <h1 className="text-xl font-bold mb-4">Add a Station</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Input placeholder="Station name" {...register("name")} />
          {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
        </div>

        <div>
          <Input placeholder="Address (optional)" {...register("address")} />
        </div>

        <div>
          <LocationPicker
            onPick={(lat, lng) => {
              setValue("lat", lat, { shouldValidate: true });
              setValue("lng", lng, { shouldValidate: true });
            }}
          />
          {(errors.lat || errors.lng) && (
            <p className="text-sm text-destructive mt-1">Please click the map to set a location</p>
          )}
        </div>

        <div>
          <Select onValueChange={(v) => setValue("connectorType", v as FormValues["connectorType"])}>
            <SelectTrigger>
              <SelectValue placeholder="Connector type" />
            </SelectTrigger>
            <SelectContent>
              {CONNECTOR_TYPES.map((type) => (
                <SelectItem key={type} value={type}>{type}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          {errors.connectorType && <p className="text-sm text-destructive">Please select a connector type</p>}
        </div>

        <div>
          <Input placeholder="Power (kW)" type="number" {...register("powerKw")} />
          {errors.powerKw && <p className="text-sm text-destructive">{errors.powerKw.message}</p>}
        </div>

        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Submitting..." : "Submit Station"}
        </Button>

        {mutation.isError && (
          <p className="text-sm text-destructive">
            {axios.isAxiosError(mutation.error) && mutation.error.response?.status === 401
              ? "Please log in to submit a station."
              : "Something went wrong. Please try again."}
          </p>
        )}
        {mutation.isSuccess && (
          <p className="text-sm text-green-600">Station submitted!</p>
        )}
      </form>
    </div>
  );
}